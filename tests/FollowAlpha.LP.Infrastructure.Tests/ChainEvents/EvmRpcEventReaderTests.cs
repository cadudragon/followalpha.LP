using FollowAlpha.LP.Application.ChainEvents;
using FollowAlpha.LP.Infrastructure.ChainEvents;
using FollowAlpha.LP.Infrastructure.Protocols;
using FluentAssertions;
using Xunit;

namespace FollowAlpha.LP.Infrastructure.Tests.ChainEvents;

public class EvmRpcEventReaderTests
{
    private const string ArbitrumNpm = "0xC36442b4a4522E871399CD717aBDD847Ab11FE88";

    private static EvmRpcEventReader Reader(FakeEvmRpc rpc) =>
        new(new ConfiguredDexProtocolRegistry(DefaultDexProtocols.UniswapV3), rpc);

    [Fact]
    public async Task Maps_each_npm_event_to_a_raw_chain_position_event()
    {
        var rpc = new FakeEvmRpc();
        rpc.Increases.Add(FakeEvmRpc.Log(new IncreaseLiquidityEventDto { TokenId = 5, Liquidity = 1000, Amount0 = 10, Amount1 = 20 }, "0xaa", 0, 100));
        rpc.Decreases.Add(FakeEvmRpc.Log(new DecreaseLiquidityEventDto { TokenId = 5, Liquidity = 400, Amount0 = 4, Amount1 = 8 }, "0xbb", 1, 101));
        rpc.Collects.Add(FakeEvmRpc.Log(new CollectEventDto { TokenId = 5, Recipient = "0xrec", Amount0 = 2, Amount1 = 3 }, "0xcc", 2, 102));
        rpc.GasByTx["0xaa"] = new GasInfo(21000, 100);
        rpc.GasByTx["0xbb"] = new GasInfo(30000, 50);
        rpc.GasByTx["0xcc"] = new GasInfo(25000, 40);
        rpc.TimestampByBlock[100] = DateTimeOffset.FromUnixTimeSeconds(1700000000);
        rpc.TimestampByBlock[101] = DateTimeOffset.FromUnixTimeSeconds(1700000100);
        rpc.TimestampByBlock[102] = DateTimeOffset.FromUnixTimeSeconds(1700000200);

        var events = await Reader(rpc).ReadPositionEventsAsync("arbitrum", 0, 1000);

        events.Should().HaveCount(3);

        var mint = events[0];
        mint.EventType.Should().Be(PositionEventTypes.Mint);
        mint.TokenId.Should().Be("5");
        mint.LiquidityDeltaRaw.Should().Be("1000");
        mint.Amount0Raw.Should().Be("10");
        mint.Amount1Raw.Should().Be("20");
        mint.NativeGasCostWei.Should().Be("2100000"); // 21000 * 100
        mint.BlockTimeUtc.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1700000000));
        mint.PositionManagerAddress.Should().Be(ArbitrumNpm);
        mint.Recipient.Should().BeNull();

        events[1].EventType.Should().Be(PositionEventTypes.Burn);
        events[1].LiquidityDeltaRaw.Should().Be("-400"); // DecreaseLiquidity → negative delta

        events[2].EventType.Should().Be(PositionEventTypes.Collect);
        events[2].LiquidityDeltaRaw.Should().Be("0"); // Collect carries no liquidity
        events[2].Recipient.Should().Be("0xrec");
    }

    [Fact]
    public async Task Orders_events_by_block_then_log_index()
    {
        var rpc = new FakeEvmRpc();
        rpc.Collects.Add(FakeEvmRpc.Log(new CollectEventDto { TokenId = 1, Recipient = "0xr", Amount0 = 1, Amount1 = 1 }, "0xc", 0, 200));
        rpc.Increases.Add(FakeEvmRpc.Log(new IncreaseLiquidityEventDto { TokenId = 1, Liquidity = 1, Amount0 = 1, Amount1 = 1 }, "0xa", 5, 100));
        rpc.Increases.Add(FakeEvmRpc.Log(new IncreaseLiquidityEventDto { TokenId = 1, Liquidity = 1, Amount0 = 1, Amount1 = 1 }, "0xa", 2, 100));
        rpc.GasByTx["0xa"] = new GasInfo(1, 1);
        rpc.GasByTx["0xc"] = new GasInfo(1, 1);
        rpc.TimestampByBlock[100] = DateTimeOffset.FromUnixTimeSeconds(1);
        rpc.TimestampByBlock[200] = DateTimeOffset.FromUnixTimeSeconds(2);

        var events = await Reader(rpc).ReadPositionEventsAsync("arbitrum", 0, 1000);

        events.Select(e => (e.BlockNumber, e.LogIndex)).Should().Equal((100, 2), (100, 5), (200, 0));
    }

    [Fact]
    public async Task Gas_and_timestamp_are_fetched_once_per_tx_and_block()
    {
        var rpc = new FakeEvmRpc();
        // Two events in the same tx and block.
        rpc.Increases.Add(FakeEvmRpc.Log(new IncreaseLiquidityEventDto { TokenId = 1, Liquidity = 1, Amount0 = 1, Amount1 = 1 }, "0xsame", 0, 100));
        rpc.Collects.Add(FakeEvmRpc.Log(new CollectEventDto { TokenId = 1, Recipient = "0xr", Amount0 = 1, Amount1 = 1 }, "0xsame", 1, 100));
        rpc.GasByTx["0xsame"] = new GasInfo(1, 1);
        rpc.TimestampByBlock[100] = DateTimeOffset.FromUnixTimeSeconds(1);

        await Reader(rpc).ReadPositionEventsAsync("arbitrum", 0, 1000);

        rpc.GasRequests.Should().ContainSingle().Which.Should().Be("0xsame");
        rpc.TimestampRequests.Should().ContainSingle().Which.Should().Be(100);
    }

    [Fact]
    public async Task Unknown_chain_is_rejected()
    {
        var act = async () => await Reader(new FakeEvmRpc()).ReadPositionEventsAsync("ethereum", 0, 1000);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
