using FollowAlpha.LP.Application.Errors;
using FollowAlpha.LP.Application.Exploration;
using FollowAlpha.LP.Application.Persistence;
using FollowAlpha.LP.Application.Tests.Collection;
using FluentAssertions;
using Xunit;

namespace FollowAlpha.LP.Application.Tests.Exploration;

public class ExplorationUseCaseTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly ExplorationPolicy Policy = ExplorationPolicy.Default;
    private const string Weth = "arbitrum:0xweth";
    private const string Usdc = "arbitrum:0xusdc";
    private const string PoolId = "arbitrum:0xpool";

    private sealed record Ctx(
        FakeExplorationReadStore Reads, InMemoryPriceStore Prices, InMemorySnapshotStore Snaps, FixedClock Clock);

    private static Ctx NewCtx()
    {
        var ctx = new Ctx(new FakeExplorationReadStore(), new InMemoryPriceStore(), new InMemorySnapshotStore(), new FixedClock(T0));
        ctx.Reads.Assets.Add(Asset(Weth, "WETH"));
        ctx.Reads.Assets.Add(Asset(Usdc, "USDC"));
        return ctx;
    }

    private static Asset Asset(string id, string symbol) =>
        new() { Id = id, ChainId = "arbitrum", Address = id, Symbol = symbol, InWatchlist = true };

    private static void SeedBars(Ctx ctx, string assetId, int count, DateTimeOffset lastOpen)
    {
        for (var i = 0; i < count; i++)
        {
            var close = 100m + ((i % 7) * 0.5m);
            ctx.Prices.Bars.Add(new PriceBar
            {
                AssetId = assetId, Resolution = "1d", OpenTimeUtc = lastOpen.AddDays(-(count - 1 - i)),
                Open = close, High = close + 1m, Low = close - 1m, Close = close, Volume = 1000m, Source = "test",
            });
        }
    }

    private static void SeedPool(Ctx ctx, DateTimeOffset snapAsOf, bool withSnapshot = true)
    {
        ctx.Reads.Pools.Add(new Pool
        {
            Id = PoolId, ChainId = "arbitrum", DexProtocolId = "uniswap-v3:arbitrum",
            Token0AssetId = Weth, Token1AssetId = Usdc, FeeTier = 500, TickSpacing = 10, Address = "0xpool", InWatchlist = true,
        });
        if (!withSnapshot)
        {
            return;
        }

        ctx.Snaps.PoolSnapshots.Add(new PoolSnapshot
        {
            PoolId = PoolId, AsOfUtc = snapAsOf, CurrentTick = 0, SqrtPriceX96 = "79228162514264337593543950336",
            Liquidity = "1000000", Tvl = 1_000_000m, DayVolumeUsd = 500_000m, Source = "test",
        });
        foreach (var (tick, gross) in new[] { (-100, "500"), (0, "1000"), (100, "500") })
        {
            ctx.Snaps.TickSnapshots.Add(new TickLiquiditySnapshot { PoolId = PoolId, AsOfUtc = snapAsOf, Tick = tick, LiquidityNet = gross, LiquidityGross = gross });
        }
    }

    // ---- ListWatchlistAssets ----

    [Fact]
    public async Task Assets_with_enough_fresh_bars_are_ok_with_a_regime()
    {
        var ctx = NewCtx();
        SeedBars(ctx, Weth, 40, T0);

        var rows = await new ListWatchlistAssets(ctx.Reads, ctx.Prices, ctx.Clock, Policy).RunAsync();

        var weth = rows.Single(r => r.Id == Weth);
        weth.DataStatus.Should().Be("OK");
        weth.Regime.Should().BeOneOf("RANGE", "TRENDING", "TRANSITION");
        weth.RvSummary.D30.Should().NotBeNull();
    }

    [Fact]
    public async Task Assets_with_thin_history_are_insufficient()
    {
        var ctx = NewCtx();
        SeedBars(ctx, Weth, 5, T0);

        var rows = await new ListWatchlistAssets(ctx.Reads, ctx.Prices, ctx.Clock, Policy).RunAsync();

        var weth = rows.Single(r => r.Id == Weth);
        weth.DataStatus.Should().Be("INSUFFICIENT");
        weth.Regime.Should().BeNull();
    }

    [Fact]
    public async Task Assets_with_only_stale_bars_are_insufficient()
    {
        var ctx = NewCtx();
        SeedBars(ctx, Weth, 40, T0.AddDays(-3)); // latest bar 3 days old > 2-day bound

        var rows = await new ListWatchlistAssets(ctx.Reads, ctx.Prices, ctx.Clock, Policy).RunAsync();

        rows.Single(r => r.Id == Weth).DataStatus.Should().Be("INSUFFICIENT");
    }

    // ---- ClassifyAssetRegime ----

    [Fact]
    public async Task Regime_returns_evidence_for_sufficient_history()
    {
        var ctx = NewCtx();
        SeedBars(ctx, Weth, 40, T0);

        var report = await new ClassifyAssetRegime(ctx.Reads, ctx.Prices, ctx.Clock, Policy).RunAsync(Weth);

        report!.Regime.Should().BeOneOf("RANGE", "TRENDING", "TRANSITION");
        report.Evidence.SampleCount.Should().Be(40);
        report.Evidence.ClassificationReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Regime_throws_insufficient_on_thin_history()
    {
        var ctx = NewCtx();
        SeedBars(ctx, Weth, 5, T0);

        var act = () => new ClassifyAssetRegime(ctx.Reads, ctx.Prices, ctx.Clock, Policy).RunAsync(Weth);

        (await act.Should().ThrowAsync<InsufficientDataException>()).Which.Missing.Should().Contain("priceBars");
    }

    [Fact]
    public async Task Regime_throws_insufficient_on_stale_history()
    {
        var ctx = NewCtx();
        SeedBars(ctx, Weth, 40, T0.AddDays(-3));

        var act = () => new ClassifyAssetRegime(ctx.Reads, ctx.Prices, ctx.Clock, Policy).RunAsync(Weth);

        (await act.Should().ThrowAsync<InsufficientDataException>()).Which.Missing.Should().Contain("freshPriceBar");
    }

    [Fact]
    public async Task Regime_for_unknown_asset_is_null()
    {
        var ctx = NewCtx();

        (await new ClassifyAssetRegime(ctx.Reads, ctx.Prices, ctx.Clock, Policy).RunAsync("arbitrum:0xnope")).Should().BeNull();
    }

    // ---- ListAssetPools ----

    [Fact]
    public async Task Pools_ok_row_computes_iv_and_competing_liquidity()
    {
        var ctx = NewCtx();
        SeedPool(ctx, T0.AddMinutes(-10));

        var rows = await new ListAssetPools(ctx.Reads, ctx.Snaps, ctx.Clock, Policy).RunAsync(Weth);

        var pool = rows!.Single();
        pool.DataStatus.Should().Be("OK");
        pool.Pair.Should().Be("WETH/USDC");
        pool.PoolIv.Annualized.Should().NotBeNull();
        pool.PoolIv.Basis.Should().Be("pool_tvl_total");
        pool.CompetingLiquidity.ActiveLiquidityAtCurrentTickRaw.Should().Be("1000000");
        pool.CompetingLiquidity.LiquidityDensityAroundPriceRaw.Should().Be("2000"); // 500+1000+500 within ±2% band
    }

    [Fact]
    public async Task Pools_stale_snapshot_is_flagged_with_nulled_metrics()
    {
        var ctx = NewCtx();
        SeedPool(ctx, T0.AddHours(-3)); // > 2h staleness bound

        var pool = (await new ListAssetPools(ctx.Reads, ctx.Snaps, ctx.Clock, Policy).RunAsync(Weth))!.Single();

        pool.DataStatus.Should().Be("STALE");
        pool.VolTvlRatio.Should().BeNull();
        pool.PoolIv.Annualized.Should().BeNull();
        pool.CompetingLiquidity.ActiveLiquidityAtCurrentTickRaw.Should().BeNull();
        pool.CompetingLiquidity.BandPct.Should().Be(Policy.CompetingLiquidityBandPct); // band still declared
    }

    [Fact]
    public async Task Pools_with_no_snapshot_are_flagged_no_snapshot()
    {
        var ctx = NewCtx();
        SeedPool(ctx, default, withSnapshot: false);

        var pool = (await new ListAssetPools(ctx.Reads, ctx.Snaps, ctx.Clock, Policy).RunAsync(Weth))!.Single();

        pool.DataStatus.Should().Be("NO_SNAPSHOT");
        pool.PoolIv.Annualized.Should().BeNull();
    }

    [Fact]
    public async Task Pools_for_unknown_asset_is_null()
    {
        var ctx = NewCtx();

        (await new ListAssetPools(ctx.Reads, ctx.Snaps, ctx.Clock, Policy).RunAsync("arbitrum:0xnope")).Should().BeNull();
    }

    // ---- GetPoolDetail ----

    [Fact]
    public async Task Pool_detail_ok_returns_snapshot_and_ticks()
    {
        var ctx = NewCtx();
        SeedPool(ctx, T0.AddMinutes(-10));

        var detail = await new GetPoolDetail(ctx.Reads, ctx.Snaps, ctx.Clock, Policy).RunAsync(PoolId);

        detail!.FeeTier.Should().Be(500);
        detail.LatestSnapshot.CurrentTick.Should().Be(0);
        detail.TickLiquidity.Should().HaveCount(3);
        detail.PoolIv.Annualized.Should().NotBeNull();
    }

    [Fact]
    public async Task Pool_detail_with_no_snapshot_is_422()
    {
        var ctx = NewCtx();
        SeedPool(ctx, default, withSnapshot: false);

        var act = () => new GetPoolDetail(ctx.Reads, ctx.Snaps, ctx.Clock, Policy).RunAsync(PoolId);

        (await act.Should().ThrowAsync<InsufficientDataException>()).Which.Missing.Should().Contain("poolSnapshot");
    }

    [Fact]
    public async Task Pool_detail_with_stale_snapshot_is_422()
    {
        var ctx = NewCtx();
        SeedPool(ctx, T0.AddHours(-3));

        var act = () => new GetPoolDetail(ctx.Reads, ctx.Snaps, ctx.Clock, Policy).RunAsync(PoolId);

        (await act.Should().ThrowAsync<InsufficientDataException>()).Which.Missing.Should().Contain("freshPoolSnapshot");
    }

    [Fact]
    public async Task Pool_detail_for_unknown_pool_is_null()
    {
        var ctx = NewCtx();

        (await new GetPoolDetail(ctx.Reads, ctx.Snaps, ctx.Clock, Policy).RunAsync("arbitrum:0xnope")).Should().BeNull();
    }

    // ---- GetAssetChart ----

    [Fact]
    public async Task Chart_returns_ordered_candles_and_a_regime_timeline()
    {
        var ctx = NewCtx();
        SeedBars(ctx, Weth, 40, T0);
        SeedPool(ctx, T0.AddMinutes(-10));

        var chart = await new GetAssetChart(ctx.Reads, ctx.Prices, ctx.Snaps, ctx.Clock, Policy).RunAsync(Weth);

        chart!.Candles.Select(c => c.OpenTimeUtc).Should().BeInAscendingOrder();
        chart.RegimeTimeline.Should().NotBeEmpty();
        chart.RvVsPoolIv.PoolIvAverage.Should().NotBeNull(); // a fresh pool exists
    }

    [Fact]
    public async Task Chart_with_no_bars_is_422()
    {
        var ctx = NewCtx();

        var act = () => new GetAssetChart(ctx.Reads, ctx.Prices, ctx.Snaps, ctx.Clock, Policy).RunAsync(Weth);

        await act.Should().ThrowAsync<InsufficientDataException>();
    }

    [Fact]
    public async Task Chart_for_unknown_asset_is_null()
    {
        var ctx = NewCtx();

        (await new GetAssetChart(ctx.Reads, ctx.Prices, ctx.Snaps, ctx.Clock, Policy).RunAsync("arbitrum:0xnope")).Should().BeNull();
    }
}
