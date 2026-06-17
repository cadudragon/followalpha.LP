using FollowAlpha.LP.Application.ChainEvents;
using FollowAlpha.LP.Application.Collection;
using FluentAssertions;
using Xunit;

namespace FollowAlpha.LP.Application.Tests.Collection;

public class SyncWalletPositionEventsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);
    private const string Token0 = "0xtoken0";
    private const string Token1 = "0xtoken1";

    private static WalletSyncRequest Request(long from = 0, long to = 1000, int reorg = 0) =>
        new("wallet1", "0xwallet", "arbitrum", from, to, reorg);

    private sealed record Harness(
        SyncWalletPositionEvents UseCase,
        FakeChainEventReader Reader,
        InMemoryPositionEventStore Store,
        InMemoryWalletOwnershipStore Ownership,
        InMemoryWalletSyncCursorStore Cursors);

    private static Harness Build()
    {
        var reader = new FakeChainEventReader();
        var state = new FakePositionStateReader();
        state.StateByToken["5"] = new ChainPositionState(-100, 100, Token0, Token1, 500);
        state.PoolAddress = "0xPOOL";
        state.DecimalsByToken[Token0] = 18;
        state.DecimalsByToken[Token1] = 6;
        var store = new InMemoryPositionEventStore();
        var ownership = new InMemoryWalletOwnershipStore();
        var cursors = new InMemoryWalletSyncCursorStore();
        return new Harness(new SyncWalletPositionEvents(reader, state, store, ownership, cursors), reader, store, ownership, cursors);
    }

    private static WalletNftTransfer In(long block, int logIndex = 0) => new("5", block, logIndex, TransferDirection.In);

    private static WalletNftTransfer Out(long block, int logIndex = 0) => new("5", block, logIndex, TransferDirection.Out);

    private static ChainPositionEvent Mint(long block, int logIndex) => new(
        "arbitrum", $"0x{block}_{logIndex}", logIndex, block, T0, "5", PositionEventTypes.Mint,
        LiquidityDeltaRaw: "1000", Amount0Raw: "1000000000000000000", Amount1Raw: "2000000",
        GasUsed: "21000", EffectiveGasPriceWei: "100", NativeGasCostWei: "2100000", Recipient: null,
        PositionManagerAddress: "0xnpm");

    private static ChainPositionEvent Collect(long block, int logIndex) => new(
        "arbitrum", $"0x{block}_{logIndex}", logIndex, block, T0, "5", PositionEventTypes.Collect,
        LiquidityDeltaRaw: "0", Amount0Raw: "500000000000000000", Amount1Raw: "1000000",
        GasUsed: "21000", EffectiveGasPriceWei: null, NativeGasCostWei: null, Recipient: "0xrecipient",
        PositionManagerAddress: "0xnpm");

    [Fact]
    public async Task Attributes_an_event_inside_the_ownership_window()
    {
        var h = Build();
        h.Reader.Transfers.Add(In(100));               // acquired at block 100
        h.Reader.Events.Add(Mint(101, logIndex: 0));   // minted while owned

        var outcome = await h.UseCase.RunAsync(Request());

        outcome.EventsInserted.Should().Be(1);
        outcome.EventsSkippedNotOwned.Should().Be(0);
        var ev = h.Store.Events.Should().ContainSingle().Subject;
        ev.WalletId.Should().Be("wallet1");
        ev.PoolId.Should().Be("arbitrum:0xpool");
        ev.Amount0.Should().Be(1m);
        h.Ownership.Intervals.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { TokenId = "5", AcquiredBlock = 100L, ReleasedBlock = (long?)null });
    }

    [Fact]
    public async Task Skips_events_after_the_nft_is_transferred_out()
    {
        var h = Build();
        h.Reader.Transfers.Add(In(100));
        h.Reader.Transfers.Add(Out(200, logIndex: 5));        // released at (200, 5)
        h.Reader.Events.Add(Mint(150, logIndex: 0));          // owned → kept
        h.Reader.Events.Add(Collect(250, logIndex: 0));       // after transfer-out → must NOT be attributed

        var outcome = await h.UseCase.RunAsync(Request());

        outcome.EventsInserted.Should().Be(1);
        outcome.EventsSkippedNotOwned.Should().Be(1);
        h.Store.Events.Should().ContainSingle().Which.EventType.Should().Be(PositionEventTypes.Mint);
    }

    [Fact]
    public async Task Same_tx_boundary_is_resolved_by_log_index()
    {
        var h = Build();
        h.Reader.Transfers.Add(In(100));
        h.Reader.Transfers.Add(Out(200, logIndex: 5));
        h.Reader.Events.Add(Collect(200, logIndex: 3));   // before the transfer-out in the same block → owned
        h.Reader.Events.Add(Collect(200, logIndex: 7));   // after the transfer-out in the same block → not owned

        var outcome = await h.UseCase.RunAsync(Request());

        outcome.EventsInserted.Should().Be(1);
        outcome.EventsSkippedNotOwned.Should().Be(1);
        h.Store.Events.Should().ContainSingle().Which.LogIndex.Should().Be(3);
    }

    [Fact]
    public async Task Skips_events_before_acquisition()
    {
        var h = Build();
        h.Reader.Transfers.Add(In(100, logIndex: 2));
        h.Reader.Events.Add(Mint(100, logIndex: 1));   // same block, before the inbound transfer → not yet owned

        var outcome = await h.UseCase.RunAsync(Request());

        outcome.EventsInserted.Should().Be(0);
        outcome.EventsSkippedNotOwned.Should().Be(1);
        h.Store.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task Rerun_over_an_overlapping_window_inserts_nothing_and_creates_no_phantom_intervals()
    {
        var h = Build();
        h.Reader.Transfers.Add(In(100));
        h.Reader.Events.Add(Mint(101, logIndex: 0));
        h.Reader.Events.Add(Collect(102, logIndex: 0));

        await h.UseCase.RunAsync(Request());
        var second = await h.UseCase.RunAsync(Request());

        second.EventsInserted.Should().Be(0);
        h.Store.Events.Should().HaveCount(2);
        h.Ownership.Intervals.Should().ContainSingle(); // the In was not re-opened as a new interval
    }

    [Fact]
    public async Task Advances_the_cursor_and_resumes_from_it()
    {
        var h = Build();
        h.Reader.Transfers.Add(In(100));
        h.Reader.Events.Add(Mint(101, logIndex: 0));

        await h.UseCase.RunAsync(Request(from: 0, to: 1000));
        h.Cursors.Cursors.Should().ContainSingle().Which.LastScannedBlock.Should().Be(1000);

        // Next run with a higher head resumes just past the cursor (reorg buffer = 0 here).
        await h.UseCase.RunAsync(Request(from: 0, to: 2000));
        h.Reader.LastReadFromBlock.Should().Be(1001);
    }

    [Fact]
    public async Task No_transfers_means_no_events_read_or_written()
    {
        var h = Build();

        var outcome = await h.UseCase.RunAsync(Request());

        outcome.TransfersSeen.Should().Be(0);
        outcome.EventsRead.Should().Be(0);
        h.Reader.ReadCallCount.Should().Be(0);
        h.Store.Events.Should().BeEmpty();
    }
}
