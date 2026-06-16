using FollowAlpha.LP.Application.Protocols;
using FollowAlpha.LP.Infrastructure.Protocols;
using FollowAlpha.LP.Infrastructure.TheGraph;
using FluentAssertions;
using Xunit;

namespace FollowAlpha.LP.Infrastructure.Tests.TheGraph;

public class TheGraphPoolDataSourceTests
{
    private const string PoolAddress = "0xAbCdEf0000000000000000000000000000000001";
    private const string LowerPoolAddress = "0xabcdef0000000000000000000000000000000001";

    [Fact]
    public async Task Get_pool_state_parses_fields_and_targets_the_chain_subgraph()
    {
        var handler = new StubHttpMessageHandler(LoadFixture("pool_state.json"));
        var source = CreateSource(handler);

        var state = await source.GetPoolStateAsync("arbitrum", PoolAddress);

        state.CurrentTick.Should().Be(-198765);
        state.SqrtPriceX96.Should().Be("4054976535745954444878957");
        state.Liquidity.Should().Be("123456789012345678");
        state.FeeTier.Should().Be(3000);
        state.TvlUsd.Should().Be(12345678.90m);

        // Targets the Arbitrum subgraph via the gateway, carries the key, and queries by lowercased id.
        handler.RequestUris[0]!.ToString().Should().Contain("subgraphs/id/FQ6JYszEKApsBpAmiHesRsd9Ygc6mzmpNRANeVQFYoVX");
        handler.RequestUris[0]!.ToString().Should().Contain("test-key");
        handler.RequestBodies[0].Should().Contain(LowerPoolAddress);
    }

    [Fact]
    public async Task Get_day_volumes_parses_dates_and_amounts()
    {
        var source = CreateSource(new StubHttpMessageHandler(LoadFixture("pool_day_data.json")));

        var volumes = await source.GetDayVolumesAsync("arbitrum", PoolAddress, days: 2);

        volumes.Should().HaveCount(2);
        volumes[0].Date.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1718064000));
        volumes[0].VolumeUsd.Should().Be(5000000.5m);
        volumes[1].VolumeUsd.Should().Be(4200000.25m);
    }

    [Fact]
    public async Task Get_tick_liquidity_paginates_with_a_progressive_cursor()
    {
        var handler = new StubHttpMessageHandler(LoadFixture("ticks_page1.json"), LoadFixture("ticks_page2.json"));
        var source = CreateSource(handler, tickPageSize: 2);

        var ticks = await source.GetTickLiquidityAsync("arbitrum", PoolAddress);

        ticks.Select(t => t.Tick).Should().Equal(-199020, -198960, -198900);
        handler.RequestUris.Should().HaveCount(2); // page1 was full (==pageSize) so a second page was fetched
        handler.RequestBodies[1].Should().Contain("\"lastTick\":\"-198960\""); // cursor = last tick of page 1
    }

    [Fact]
    public async Task Graphql_errors_surface_as_a_query_exception()
    {
        var source = CreateSource(new StubHttpMessageHandler(LoadFixture("graph_errors.json")));

        var act = async () => await source.GetPoolStateAsync("arbitrum", PoolAddress);

        await act.Should().ThrowAsync<TheGraphQueryException>();
    }

    [Fact]
    public async Task A_missing_pool_throws_pool_not_found()
    {
        var source = CreateSource(new StubHttpMessageHandler(LoadFixture("pool_not_found.json")));

        var act = async () => await source.GetPoolStateAsync("arbitrum", PoolAddress);

        await act.Should().ThrowAsync<PoolNotFoundException>();
    }

    [Fact]
    public async Task A_pinned_deployment_id_is_preferred_in_the_url()
    {
        var registry = new ConfiguredDexProtocolRegistry(
        [
            new DexProtocolDescriptor("arbitrum", "uniswap-v3", "SUBID", "QmDeploymentPinned", "0xpm", [3000], "test", new DateOnly(2026, 6, 16)),
        ]);
        var handler = new StubHttpMessageHandler(LoadFixture("pool_state.json"));
        var source = new TheGraphPoolDataSource(new HttpClient(handler), registry, new TheGraphGatewayOptions { ApiKey = "test-key" });

        await source.GetPoolStateAsync("arbitrum", PoolAddress);

        handler.RequestUris[0]!.ToString().Should().Contain("deployments/id/QmDeploymentPinned");
        handler.RequestUris[0]!.ToString().Should().NotContain("subgraphs/id/");
    }

    [Fact]
    public async Task An_unknown_chain_is_rejected()
    {
        var source = CreateSource(new StubHttpMessageHandler(LoadFixture("pool_state.json")));

        var act = async () => await source.GetPoolStateAsync("ethereum", PoolAddress);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Day_volumes_rejects_non_positive_days()
    {
        var source = CreateSource(new StubHttpMessageHandler());

        var act = async () => await source.GetDayVolumesAsync("arbitrum", PoolAddress, days: 0);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    private static TheGraphPoolDataSource CreateSource(StubHttpMessageHandler handler, int tickPageSize = 1000)
    {
        var registry = new ConfiguredDexProtocolRegistry(DefaultDexProtocols.UniswapV3);
        var options = new TheGraphGatewayOptions { ApiKey = "test-key", TickPageSize = tickPageSize };
        return new TheGraphPoolDataSource(new HttpClient(handler), registry, options);
    }

    private static string LoadFixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TheGraph", "Fixtures", name));
}
