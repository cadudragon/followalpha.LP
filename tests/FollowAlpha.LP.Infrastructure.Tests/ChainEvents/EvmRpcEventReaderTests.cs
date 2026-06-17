using FollowAlpha.LP.Application.ChainEvents;
using FollowAlpha.LP.Infrastructure.ChainEvents;
using FollowAlpha.LP.Infrastructure.Protocols;
using FluentAssertions;
using Xunit;

namespace FollowAlpha.LP.Infrastructure.Tests.ChainEvents;

public class EvmRpcEventReaderTests
{
    private const string ArbitrumNpm = "0xC36442b4a4522E871399CD717aBDD847Ab11FE88";
    private const string Recipient = "0x1111111111111111111111111111111111111111";
    private static readonly string[] TokenId5 = ["5"];

    private static EvmRpcEventReader Reader(FakeEvmRpc rpc) =>
        new(new ConfiguredDexProtocolRegistry(DefaultDexProtocols.UniswapV3), rpc);

    [Fact]
    public async Task Decodes_and_maps_npm_logs_for_the_requested_token_ids()
    {
        var rpc = new FakeEvmRpc();
        rpc.AddLog(NpmLogFactory.Topic<IncreaseLiquidityEventDto>(), NpmLogFactory.Increase(5, 1000, 10, 20, "0xaa", 0, 100));
        rpc.AddLog(NpmLogFactory.Topic<DecreaseLiquidityEventDto>(), NpmLogFactory.Decrease(5, 400, 4, 8, "0xbb", 1, 101));
        rpc.AddLog(NpmLogFactory.Topic<CollectEventDto>(), NpmLogFactory.Collect(5, Recipient, 2, 3, "0xcc", 2, 102));
        rpc.GasByTx["0xaa"] = new GasInfo(21000, 100);
        rpc.GasByTx["0xbb"] = new GasInfo(30000, 50);
        rpc.GasByTx["0xcc"] = new GasInfo(25000, 40);
        rpc.TimestampByBlock[100] = DateTimeOffset.FromUnixTimeSeconds(1700000000);
        rpc.TimestampByBlock[101] = DateTimeOffset.FromUnixTimeSeconds(1700000100);
        rpc.TimestampByBlock[102] = DateTimeOffset.FromUnixTimeSeconds(1700000200);

        var events = await Reader(rpc).ReadPositionEventsAsync("arbitrum", TokenId5, 0, 1000);

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
        events[2].LiquidityDeltaRaw.Should().Be("0");
        events[2].Recipient!.ToLowerInvariant().Should().Be(Recipient); // decoded from log data
    }

    [Fact]
    public async Task Filters_by_token_id_and_never_scans_globally()
    {
        var rpc = new FakeEvmRpc();
        rpc.AddLog(NpmLogFactory.Topic<IncreaseLiquidityEventDto>(), NpmLogFactory.Increase(5, 1, 1, 1, "0xaa", 0, 100));
        rpc.GasByTx["0xaa"] = new GasInfo(1, 1);
        rpc.TimestampByBlock[100] = DateTimeOffset.FromUnixTimeSeconds(1);

        await Reader(rpc).ReadPositionEventsAsync("arbitrum", TokenId5, 0, 1000);

        rpc.LogRequests.Should().OnlyContain(r => r.TokenIds.Contains("5"));
    }

    [Fact]
    public async Task No_token_ids_returns_empty_without_any_rpc_call()
    {
        var rpc = new FakeEvmRpc();

        var events = await Reader(rpc).ReadPositionEventsAsync("arbitrum", [], 0, 1000);

        events.Should().BeEmpty();
        rpc.LogRequests.Should().BeEmpty(); // never a global scan
    }

    [Fact]
    public async Task Unknown_effective_gas_price_is_reported_as_unknown_not_zero()
    {
        var rpc = new FakeEvmRpc();
        rpc.AddLog(NpmLogFactory.Topic<IncreaseLiquidityEventDto>(), NpmLogFactory.Increase(5, 1, 1, 1, "0xaa", 0, 100));
        rpc.GasByTx["0xaa"] = new GasInfo(21000, EffectiveGasPriceWei: null);
        rpc.TimestampByBlock[100] = DateTimeOffset.FromUnixTimeSeconds(1);

        var events = await Reader(rpc).ReadPositionEventsAsync("arbitrum", TokenId5, 0, 1000);

        events[0].GasUsed.Should().Be("21000");
        events[0].EffectiveGasPriceWei.Should().BeNull();
        events[0].NativeGasCostWei.Should().BeNull();
    }

    [Fact]
    public async Task Orders_events_by_block_then_log_index()
    {
        var rpc = new FakeEvmRpc();
        rpc.AddLog(NpmLogFactory.Topic<CollectEventDto>(), NpmLogFactory.Collect(5, Recipient, 1, 1, "0xc", 0, 200));
        rpc.AddLog(NpmLogFactory.Topic<IncreaseLiquidityEventDto>(), NpmLogFactory.Increase(5, 1, 1, 1, "0xa", 5, 100));
        rpc.AddLog(NpmLogFactory.Topic<IncreaseLiquidityEventDto>(), NpmLogFactory.Increase(5, 1, 1, 1, "0xa", 2, 100));
        rpc.GasByTx["0xa"] = new GasInfo(1, 1);
        rpc.GasByTx["0xc"] = new GasInfo(1, 1);
        rpc.TimestampByBlock[100] = DateTimeOffset.FromUnixTimeSeconds(1);
        rpc.TimestampByBlock[200] = DateTimeOffset.FromUnixTimeSeconds(2);

        var events = await Reader(rpc).ReadPositionEventsAsync("arbitrum", TokenId5, 0, 1000);

        events.Select(e => (e.BlockNumber, e.LogIndex)).Should().Equal((100, 2), (100, 5), (200, 0));
    }

    [Fact]
    public async Task Gas_and_timestamp_are_fetched_once_per_tx_and_block()
    {
        var rpc = new FakeEvmRpc();
        rpc.AddLog(NpmLogFactory.Topic<IncreaseLiquidityEventDto>(), NpmLogFactory.Increase(5, 1, 1, 1, "0xsame", 0, 100));
        rpc.AddLog(NpmLogFactory.Topic<CollectEventDto>(), NpmLogFactory.Collect(5, Recipient, 1, 1, "0xsame", 1, 100));
        rpc.GasByTx["0xsame"] = new GasInfo(1, 1);
        rpc.TimestampByBlock[100] = DateTimeOffset.FromUnixTimeSeconds(1);

        await Reader(rpc).ReadPositionEventsAsync("arbitrum", TokenId5, 0, 1000);

        rpc.GasRequests.Should().ContainSingle().Which.Should().Be("0xsame");
        rpc.TimestampRequests.Should().ContainSingle().Which.Should().Be(100);
    }

    [Fact]
    public async Task Rejects_inverted_block_range()
    {
        var act = async () => await Reader(new FakeEvmRpc()).ReadPositionEventsAsync("arbitrum", TokenId5, 1000, 0);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Unknown_chain_is_rejected()
    {
        var act = async () => await Reader(new FakeEvmRpc()).ReadPositionEventsAsync("ethereum", TokenId5, 0, 1000);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Discovers_inbound_and_outbound_transfers_with_direction_ordered()
    {
        const string Wallet = "0x2222222222222222222222222222222222222222";
        const string Other = "0x3333333333333333333333333333333333333333";
        var rpc = new FakeEvmRpc();
        rpc.InboundTransferLogs.Add(NpmLogFactory.Transfer(5, from: Other, to: Wallet, blockNumber: 100, logIndex: 1));
        rpc.OutboundTransferLogs.Add(NpmLogFactory.Transfer(5, from: Wallet, to: Other, blockNumber: 200, logIndex: 3));

        var transfers = await Reader(rpc).DiscoverWalletTransfersAsync("arbitrum", Wallet, 0, 1000);

        transfers.Select(t => (t.TokenId, t.BlockNumber, t.LogIndex, t.Direction)).Should().Equal(
            ("5", 100, 1, TransferDirection.In),
            ("5", 200, 3, TransferDirection.Out));

        // Each direction is queried with its own filter (a single eth_getLogs ANDs topics).
        rpc.TransferRequests.Should().Contain((null, Wallet)).And.Contain((Wallet, null));
    }
}
