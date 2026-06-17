using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FollowAlpha.LP.Api.Security;
using FollowAlpha.LP.Application.Persistence;
using FollowAlpha.LP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FollowAlpha.LP.Api.Tests;

/// <summary>
/// Phase 3.1 foundation + 3.2 exploration: the host boots, /health answers, the X-Api-Key gate accepts/rejects
/// (now via a real endpoint), errors are RFC 7807 problem+json, unknown ids are 404 and thin/stale data is 422,
/// and the OpenAPI document exposes the new endpoints. Backed by a seeded temp SQLite DB.
/// </summary>
public sealed class ApiSkeletonTests : IClassFixture<ApiSkeletonTests.Factory>
{
    private const string ValidKey = "test-key";
    private const string Weth = "arbitrum:0xweth";
    private const string Thin = "arbitrum:0xthin";
    private const string PoolId = "arbitrum:0xpool";
    private readonly Factory _factory;

    public ApiSkeletonTests(Factory factory) => _factory = factory;

    public sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"lp-api-test-{Guid.NewGuid():N}.db");

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LP_API_KEY"] = ValidKey,
                ["LP_DB_PATH"] = _dbPath,
            }));

            var host = base.CreateHost(builder);
            using (var scope = host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureCreated();
                Seed(db);
            }

            return host;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
            {
                return;
            }

            // SQLite pools the file connection; release the handle before deleting the temp DB (best-effort).
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try
            {
                if (File.Exists(_dbPath))
                {
                    File.Delete(_dbPath);
                }
            }
            catch (IOException)
            {
                // A transient lock on teardown must not fail the suite; the temp file is reaped by the OS.
            }
        }
    }

    private HttpClient Authed()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyEndpointFilter.HeaderName, ValidKey);
        return client;
    }

    [Fact]
    public async Task Health_is_anonymous_and_returns_ok()
    {
        var response = await _factory.CreateClient().GetAsync("/v1/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString().Should().Be("ok");
    }

    [Fact]
    public async Task Secured_endpoint_without_api_key_is_401_problem_json()
    {
        var response = await _factory.CreateClient().GetAsync("/v1/assets");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Secured_endpoint_with_wrong_api_key_is_401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyEndpointFilter.HeaderName, "not-the-key");

        (await client.GetAsync("/v1/assets")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Assets_lists_watchlist_with_regime_and_data_status()
    {
        var assets = await Authed().GetFromJsonAsync<JsonElement>("/v1/assets");

        assets.ValueKind.Should().Be(JsonValueKind.Array);
        var weth = assets.EnumerateArray().Single(a => a.GetProperty("id").GetString() == Weth);
        weth.GetProperty("symbol").GetString().Should().Be("WETH");
        weth.GetProperty("dataStatus").GetString().Should().Be("OK");
        weth.GetProperty("regime").GetString().Should().BeOneOf("RANGE", "TRENDING", "TRANSITION");

        var thin = assets.EnumerateArray().Single(a => a.GetProperty("id").GetString() == Thin);
        thin.GetProperty("dataStatus").GetString().Should().Be("INSUFFICIENT");
        thin.GetProperty("regime").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Regime_returns_descriptive_label_with_evidence_no_direction()
    {
        var regime = await Authed().GetFromJsonAsync<JsonElement>($"/v1/assets/{Weth}/regime");

        regime.GetProperty("regime").GetString().Should().BeOneOf("RANGE", "TRENDING", "TRANSITION");
        var evidence = regime.GetProperty("evidence");
        evidence.GetProperty("minBars").GetInt32().Should().BePositive();
        evidence.GetProperty("sampleCount").GetInt32().Should().BeGreaterThanOrEqualTo(evidence.GetProperty("minBars").GetInt32());
        evidence.GetProperty("classificationReason").GetString().Should().NotBeNullOrWhiteSpace();
        // RN-07: never a direction word.
        regime.GetRawText().ToLowerInvariant().Should().NotContainAny("bullish", "bearish", "long", "short");
    }

    [Fact]
    public async Task Regime_on_thin_history_is_422_problem_json()
    {
        var response = await Authed().GetAsync($"/v1/assets/{Thin}/regime");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("missing")
            .EnumerateArray().Select(e => e.GetString()).Should().Contain("priceBars");
    }

    [Fact]
    public async Task Unknown_asset_is_404_problem_json()
    {
        var response = await Authed().GetAsync("/v1/assets/arbitrum:0xunknown/regime");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Chart_returns_ordered_candles_and_regime_timeline()
    {
        var chart = await Authed().GetFromJsonAsync<JsonElement>($"/v1/assets/{Weth}/chart");

        var times = chart.GetProperty("candles").EnumerateArray().Select(c => c.GetProperty("openTimeUtc").GetDateTimeOffset()).ToList();
        times.Should().BeInAscendingOrder().And.NotBeEmpty();
        chart.GetProperty("regimeTimeline").GetArrayLength().Should().BePositive();
        chart.GetProperty("rvVsPoolIv").GetProperty("ivBasis").GetString().Should().Be("pool_tvl_total");
    }

    [Fact]
    public async Task Pools_table_exposes_fee_tier_volume_tvl_iv_and_competing_liquidity()
    {
        var pools = await Authed().GetFromJsonAsync<JsonElement>($"/v1/assets/{Weth}/pools");

        var pool = pools.EnumerateArray().Single(p => p.GetProperty("poolId").GetString() == PoolId);
        pool.GetProperty("feeTier").GetInt32().Should().Be(500);
        pool.GetProperty("dataStatus").GetString().Should().Be("OK");
        pool.GetProperty("volTvlRatio").ValueKind.Should().NotBe(JsonValueKind.Null);

        var iv = pool.GetProperty("poolIv");
        iv.GetProperty("basis").GetString().Should().Be("pool_tvl_total");
        iv.GetProperty("annualized").ValueKind.Should().NotBe(JsonValueKind.Null);

        var cl = pool.GetProperty("competingLiquidity");
        cl.GetProperty("activeLiquidityAtCurrentTickRaw").GetString().Should().NotBeNullOrEmpty();
        cl.GetProperty("liquidityDensityAroundPriceRaw").GetString().Should().NotBeNullOrEmpty();
        cl.GetProperty("bandPct").GetDecimal().Should().BeGreaterThan(0m);
    }

    [Fact]
    public async Task Pool_detail_returns_latest_snapshot_and_tick_distribution()
    {
        var detail = await Authed().GetFromJsonAsync<JsonElement>($"/v1/pools/{PoolId}");

        detail.GetProperty("feeTier").GetInt32().Should().Be(500);
        detail.GetProperty("latestSnapshot").GetProperty("currentTick").GetInt32().Should().Be(0);
        detail.GetProperty("tickLiquidity").GetArrayLength().Should().BePositive();
        detail.GetProperty("poolIv").GetProperty("basis").GetString().Should().Be("pool_tvl_total");
    }

    [Fact]
    public async Task OpenApi_exposes_the_exploration_endpoints()
    {
        var body = await _factory.CreateClient().GetStringAsync("/openapi/v1.json");

        body.Should().Contain("/v1/assets").And.Contain("/v1/pools/{poolId}");
    }

    private static void Seed(AppDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        db.Chains.Add(new Chain { Id = "arbitrum", Name = "Arbitrum One", RpcEnvVarName = "RPC_URL_ARBITRUM", Enabled = true });
        db.DexProtocols.Add(new DexProtocol { Id = "uniswap-v3:arbitrum", ChainId = "arbitrum", SubgraphId = "sub", PositionManagerAddress = "0xpm", FeeTiers = "[500]", Enabled = true });
        db.Assets.Add(new Asset { Id = Weth, ChainId = "arbitrum", Address = "0xweth", Symbol = "WETH", Decimals = 18, InWatchlist = true });
        db.Assets.Add(new Asset { Id = "arbitrum:0xusdc", ChainId = "arbitrum", Address = "0xusdc", Symbol = "USDC", Decimals = 6, InWatchlist = true });
        db.Assets.Add(new Asset { Id = Thin, ChainId = "arbitrum", Address = "0xthin", Symbol = "THIN", Decimals = 18, InWatchlist = true });

        // WETH: 40 fresh daily bars (enough for a regime); THIN: 2 bars (insufficient).
        for (var i = 0; i < 40; i++)
        {
            db.PriceBars.Add(Bar(Weth, now.AddDays(-(39 - i)), 100m + ((i % 7) * 0.5m)));
        }

        db.PriceBars.Add(Bar(Thin, now.AddDays(-1), 100m));
        db.PriceBars.Add(Bar(Thin, now, 101m));

        db.Pools.Add(new Pool
        {
            Id = PoolId, ChainId = "arbitrum", DexProtocolId = "uniswap-v3:arbitrum",
            Token0AssetId = Weth, Token1AssetId = "arbitrum:0xusdc", FeeTier = 500, TickSpacing = 10,
            Address = "0xpool", InWatchlist = true,
        });
        db.PoolSnapshots.Add(new PoolSnapshot
        {
            PoolId = PoolId, AsOfUtc = now, CurrentTick = 0, SqrtPriceX96 = "79228162514264337593543950336",
            Liquidity = "1000000", Tvl = 1_000_000m, DayVolumeUsd = 500_000m, Source = "test",
        });
        foreach (var (tick, gross) in new[] { (-100, "500"), (0, "1000"), (100, "500") })
        {
            db.TickLiquiditySnapshots.Add(new TickLiquiditySnapshot { PoolId = PoolId, AsOfUtc = now, Tick = tick, LiquidityNet = gross, LiquidityGross = gross });
        }

        db.SaveChanges();
    }

    private static PriceBar Bar(string assetId, DateTimeOffset open, decimal close) => new()
    {
        AssetId = assetId, Resolution = "1d", OpenTimeUtc = open,
        Open = close, High = close + 1m, Low = close - 1m, Close = close, Volume = 1000m, Source = "test",
    };
}
