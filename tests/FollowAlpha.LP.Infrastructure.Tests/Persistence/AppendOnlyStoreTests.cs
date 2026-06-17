using FollowAlpha.LP.Application;
using FollowAlpha.LP.Application.Persistence;
using FollowAlpha.LP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FollowAlpha.LP.Infrastructure.Tests.Persistence;

/// <summary>
/// Integration tests against a migrated in-memory SQLite database: idempotent insert-if-absent for facts
/// (natural keys), append-by-identity for decision/intent (RN-03 — not de-duplicated), and the position
/// projection upsert.
/// </summary>
public class AppendOnlyStoreTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Price_bar_insert_is_idempotent_on_its_natural_key()
    {
        using var db = new TestDatabase();
        var store = new EfPriceStore(db.Context);

        var first = await store.InsertIfAbsentAsync(new PriceBar
        {
            AssetId = "ETH", Resolution = "1d", OpenTimeUtc = T0,
            Open = 1m, High = 2m, Low = 0.5m, Close = 1.5m, Volume = 100m, Source = "coinbase",
        });

        // Same natural key, different non-key fields — must NOT create a second row.
        var second = await store.InsertIfAbsentAsync(new PriceBar
        {
            AssetId = "ETH", Resolution = "1d", OpenTimeUtc = T0,
            Open = 9m, High = 9m, Low = 9m, Close = 9m, Volume = 9m, Source = "binance",
        });

        first.Should().BeTrue();
        second.Should().BeFalse();
        var bars = await store.GetByAssetAsync(Tenancy.DefaultTenantId, "ETH", "1d");
        bars.Should().HaveCount(1);
        bars[0].Source.Should().Be("coinbase"); // original preserved, not overwritten
    }

    [Fact]
    public async Task Pool_and_tick_snapshots_are_idempotent_on_their_natural_keys()
    {
        using var db = new TestDatabase();
        var store = new EfSnapshotStore(db.Context);

        var pool1 = await store.InsertPoolSnapshotIfAbsentAsync(NewPoolSnapshot());
        var pool2 = await store.InsertPoolSnapshotIfAbsentAsync(NewPoolSnapshot());
        pool1.Should().BeTrue();
        pool2.Should().BeFalse();
        (await store.GetLatestPoolSnapshotAsync(Tenancy.DefaultTenantId, "pool1")).Should().NotBeNull();

        var tick1 = await store.InsertTickLiquiditySnapshotIfAbsentAsync(NewTick(100));
        var tick1Again = await store.InsertTickLiquiditySnapshotIfAbsentAsync(NewTick(100));
        var tick2 = await store.InsertTickLiquiditySnapshotIfAbsentAsync(NewTick(200));
        tick1.Should().BeTrue();
        tick1Again.Should().BeFalse();
        tick2.Should().BeTrue();
        (await store.GetTickLiquidityAsync(Tenancy.DefaultTenantId, "pool1", T0)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Position_event_insert_is_idempotent_on_chain_tx_logindex()
    {
        using var db = new TestDatabase();
        var store = new EfPositionEventStore(db.Context);

        var first = await store.InsertIfAbsentAsync(NewEvent());
        var second = await store.InsertIfAbsentAsync(NewEvent());
        first.Should().BeTrue();
        second.Should().BeFalse();
        (await store.GetByWalletAsync(Tenancy.DefaultTenantId, "wallet1")).Should().HaveCount(1);
    }

    [Fact]
    public async Task Decision_entries_are_appended_not_deduplicated_by_content_hash()
    {
        using var db = new TestDatabase();
        var log = new EfDecisionLog(db.Context);

        // Two evaluations with identical content (same ContentHash) at different times are two events (RN-03).
        await log.AppendEntryAsync(NewDecision(Guid.NewGuid(), "same-hash", T0));
        await log.AppendEntryAsync(NewDecision(Guid.NewGuid(), "same-hash", T1));

        (await log.GetEntriesByPoolAsync(Tenancy.DefaultTenantId, "pool1")).Should().HaveCount(2);
    }

    [Fact]
    public async Task Decision_append_rejects_empty_id()
    {
        using var db = new TestDatabase();
        var log = new EfDecisionLog(db.Context);

        var act = async () => await log.AppendEntryAsync(NewDecision(Guid.Empty, "h", T0));
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Intent_records_append_as_a_history()
    {
        using var db = new TestDatabase();
        await SeedPositionAsync(db, "pos1");
        var store = new EfIntentRecordStore(db.Context);

        var original = Guid.NewGuid();
        await store.AppendAsync(new IntentRecord { Id = original, PositionId = "pos1", Intent = "HARVEST", DeclaredAtUtc = T0 });
        await store.AppendAsync(new IntentRecord { Id = Guid.NewGuid(), PositionId = "pos1", Intent = "ACCUMULATE", DeclaredAtUtc = T1, SupersedesIntentRecordId = original });

        var history = await store.GetByPositionAsync(Tenancy.DefaultTenantId, "pos1");
        history.Should().HaveCount(2);
        history[0].Intent.Should().Be("HARVEST"); // oldest first; original preserved
        history[1].SupersedesIntentRecordId.Should().Be(original);
    }

    [Fact]
    public async Task Position_projection_upserts()
    {
        using var db = new TestDatabase();
        var store = new EfPositionStore(db.Context);

        await store.UpsertAsync(new Position { Id = "pos1", WalletId = "wallet1", PoolId = "pool1", Status = "OPEN", OpenedAtUtc = T0 });
        await store.UpsertAsync(new Position { Id = "pos1", WalletId = "wallet1", PoolId = "pool1", Status = "CLOSED", OpenedAtUtc = T0, ClosedAtUtc = T1 });

        var position = await store.GetAsync(Tenancy.DefaultTenantId, "pos1");
        position!.Status.Should().Be("CLOSED");
        (await store.GetByWalletAsync(Tenancy.DefaultTenantId, "wallet1")).Should().HaveCount(1);
    }

    [Fact]
    public async Task Annotation_for_a_missing_entry_is_rejected_by_the_foreign_key()
    {
        using var db = new TestDatabase();
        var log = new EfDecisionLog(db.Context);

        var act = async () => await log.AppendAnnotationAsync(new DecisionAnnotation
        {
            Id = Guid.NewGuid(), DecisionLogEntryId = Guid.NewGuid(), CreatedAtUtc = T0, Text = "orphan",
        });

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Intent_superseding_a_missing_record_is_rejected_by_the_foreign_key()
    {
        using var db = new TestDatabase();
        await SeedPositionAsync(db, "pos1");
        var store = new EfIntentRecordStore(db.Context);

        var act = async () => await store.AppendAsync(new IntentRecord
        {
            Id = Guid.NewGuid(), PositionId = "pos1", Intent = "ACCUMULATE", DeclaredAtUtc = T0, SupersedesIntentRecordId = Guid.NewGuid(),
        });

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Pool_snapshot_for_a_missing_pool_is_rejected_by_the_foreign_key()
    {
        using var db = new TestDatabase();
        var store = new EfSnapshotStore(db.Context);

        var snapshot = NewPoolSnapshot();
        snapshot.PoolId = "ghost-pool";

        var act = async () => await store.InsertPoolSnapshotIfAbsentAsync(snapshot);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    private static async Task SeedPositionAsync(TestDatabase db, string id)
    {
        db.Context.Positions.Add(new Position
        {
            Id = id, WalletId = TestDatabase.WalletId, PoolId = TestDatabase.PoolId, Status = "OPEN", OpenedAtUtc = T0,
        });
        await db.Context.SaveChangesAsync();
    }

    private static PoolSnapshot NewPoolSnapshot() => new()
    {
        PoolId = "pool1", AsOfUtc = T0, CurrentTick = 0,
        SqrtPriceX96 = "79228162514264337593543950336", Liquidity = "1000000", Tvl = 1m, DayVolumeUsd = 1m, Source = "thegraph",
    };

    private static TickLiquiditySnapshot NewTick(int tick) => new()
    {
        PoolId = "pool1", AsOfUtc = T0, Tick = tick, LiquidityNet = "1", LiquidityGross = "1",
    };

    private static PositionEvent NewEvent() => new()
    {
        ChainId = "arbitrum", TxHash = "0xabc", LogIndex = 0, WalletId = "wallet1", PoolId = "pool1",
        EventType = "MINT", TickLower = -100, TickUpper = 100, LiquidityDelta = "1000",
        Amount0 = 1m, Amount1 = 1m, FeesCollected0 = 0m, FeesCollected1 = 0m,
        GasUsed = "21000", EffectiveGasPriceWei = "1000000000", NativeGasCostWei = "21000000000000", GasCostUsd = null, BlockTimeUtc = T0,
    };

    private static DecisionLogEntry NewDecision(Guid id, string hash, DateTimeOffset createdAt) => new()
    {
        Id = id, CreatedAtUtc = createdAt, Kind = "RANGE_VERDICT", PoolId = "pool1", Intent = "HARVEST",
        Capital = 1000m, TickLower = -100, TickUpper = 100, InputsJson = "{}", Verdict = "DONT_OPEN",
        ExpectancyNet = 0m, ContentHash = hash,
    };
}
