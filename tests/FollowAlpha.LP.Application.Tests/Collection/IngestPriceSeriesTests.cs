using FollowAlpha.LP.Application.Collection;
using FollowAlpha.LP.Application.Prices;
using FluentAssertions;
using Xunit;

namespace FollowAlpha.LP.Application.Tests.Collection;

public class IngestPriceSeriesTests
{
    private static readonly DateTimeOffset D0 = new(2026, 6, 15, 0, 0, 0, TimeSpan.Zero);
    private static readonly AssetToPrice Weth = new("arbitrum:0xweth", "arbitrum", "0xweth");

    [Fact]
    public async Task Persists_daily_usd_bars_as_price_bars()
    {
        var source = new FakePriceSeriesSource();
        source.Bars.Add(new AssetUsdBar(D0, 2500m, 2600m, 2450m, 2550m, 1_000_000m));
        var store = new InMemoryPriceStore();
        var useCase = new IngestPriceSeries(source, store);

        var outcomes = await useCase.RunAsync([Weth], days: 30);

        outcomes.Should().ContainSingle();
        outcomes[0].BarsInserted.Should().Be(1);
        var bar = store.Bars.Should().ContainSingle().Subject;
        bar.AssetId.Should().Be("arbitrum:0xweth");
        bar.Resolution.Should().Be(IngestPriceSeries.Resolution);
        bar.Source.Should().Be("thegraph");
        bar.OpenTimeUtc.Should().Be(D0);
        bar.Close.Should().Be(2550m);
        bar.Volume.Should().Be(1_000_000m);
    }

    [Fact]
    public async Task Rerun_inserts_nothing_new()
    {
        var source = new FakePriceSeriesSource();
        source.Bars.Add(new AssetUsdBar(D0, 2500m, 2600m, 2450m, 2550m, 1_000_000m));
        source.Bars.Add(new AssetUsdBar(D0.AddDays(1), 2550m, 2700m, 2500m, 2680m, 1_200_000m));
        var store = new InMemoryPriceStore();
        var useCase = new IngestPriceSeries(source, store);

        await useCase.RunAsync([Weth], days: 30);
        var second = await useCase.RunAsync([Weth], days: 30);

        second[0].BarsInserted.Should().Be(0);
        store.Bars.Should().HaveCount(2);
    }

    [Fact]
    public async Task One_failing_asset_is_recorded_and_does_not_abort_the_batch()
    {
        var source = new FakePriceSeriesSource();
        source.Bars.Add(new AssetUsdBar(D0, 2500m, 2600m, 2450m, 2550m, 1_000_000m));
        source.FailFor = addr => addr == "0xbad" ? new InvalidOperationException("boom") : null;
        var useCase = new IngestPriceSeries(source, new InMemoryPriceStore());

        var outcomes = await useCase.RunAsync(
            [new AssetToPrice("arbitrum:0xbad", "arbitrum", "0xbad"), Weth], days: 30);

        outcomes.Should().HaveCount(2);
        outcomes[0].Error.Should().Be("boom");
        outcomes[1].Error.Should().BeNull();
    }

    [Fact]
    public async Task Rejects_non_positive_days()
    {
        var useCase = new IngestPriceSeries(new FakePriceSeriesSource(), new InMemoryPriceStore());
        var act = async () => await useCase.RunAsync([Weth], days: 0);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
