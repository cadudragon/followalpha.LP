namespace FollowAlpha.LP.Application.Persistence;

// Persistence ports, grouped by write semantics (ARCHITECTURE.md §5). The append-only stores expose
// insert/append + query only — no update/delete — which an architecture test asserts by interface shape
// (DATA-MODEL.md §4). Facts are idempotent insert-if-absent on natural keys; decision/intent records are
// append-by-identity (RN-03 — never de-duplicated). IPositionStore is the rebuildable projection (upsert).

/// <summary>Append-only store for spot OHLCV bars (fact).</summary>
public interface IPriceStore
{
    /// <summary>Inserts the bar if its natural key is not already present. Returns true if inserted.</summary>
    Task<bool> InsertIfAbsentAsync(PriceBar bar, CancellationToken cancellationToken = default);

    /// <summary>All bars for an asset at a resolution, oldest first.</summary>
    Task<IReadOnlyList<PriceBar>> GetByAssetAsync(string tenantId, string assetId, string resolution, CancellationToken cancellationToken = default);
}

/// <summary>Append-only store for pool and per-tick liquidity snapshots (facts). Separate methods: tick liquidity is the irrecoverable datum.</summary>
public interface ISnapshotStore
{
    /// <summary>Inserts a pool snapshot if (PoolId, AsOfUtc) is not present. Returns true if inserted.</summary>
    Task<bool> InsertPoolSnapshotIfAbsentAsync(PoolSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>Inserts a tick-liquidity snapshot if (PoolId, AsOfUtc, Tick) is not present. Returns true if inserted.</summary>
    Task<bool> InsertTickLiquiditySnapshotIfAbsentAsync(TickLiquiditySnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>The most recent pool snapshot for a pool, or null.</summary>
    Task<PoolSnapshot?> GetLatestPoolSnapshotAsync(string tenantId, string poolId, CancellationToken cancellationToken = default);

    /// <summary>The per-tick liquidity distribution captured at a moment.</summary>
    Task<IReadOnlyList<TickLiquiditySnapshot>> GetTickLiquidityAsync(string tenantId, string poolId, DateTimeOffset asOfUtc, CancellationToken cancellationToken = default);
}

/// <summary>Append-only store for on-chain position events (fact).</summary>
public interface IPositionEventStore
{
    /// <summary>Inserts the event if (ChainId, TxHash, LogIndex) is not present. Returns true if inserted.</summary>
    Task<bool> InsertIfAbsentAsync(PositionEvent positionEvent, CancellationToken cancellationToken = default);

    /// <summary>All events for a wallet, oldest first.</summary>
    Task<IReadOnlyList<PositionEvent>> GetByWalletAsync(string tenantId, string walletId, CancellationToken cancellationToken = default);
}

/// <summary>Append-only store for the intent history (RN-01). Records are appended by identity, never de-duplicated.</summary>
public interface IIntentRecordStore
{
    /// <summary>Appends an intent record.</summary>
    Task AppendAsync(IntentRecord record, CancellationToken cancellationToken = default);

    /// <summary>The full intent history for a position, oldest first.</summary>
    Task<IReadOnlyList<IntentRecord>> GetByPositionAsync(string tenantId, string positionId, CancellationToken cancellationToken = default);
}

/// <summary>Append-only decision log (RN-03): every evaluation is recorded; annotations are added, never edited.</summary>
public interface IDecisionLog
{
    /// <summary>Appends a decision-log entry.</summary>
    Task AppendEntryAsync(DecisionLogEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Appends a dated annotation to an existing entry.</summary>
    Task AppendAnnotationAsync(DecisionAnnotation annotation, CancellationToken cancellationToken = default);

    /// <summary>A single decision-log entry by id, or null.</summary>
    Task<DecisionLogEntry?> GetEntryAsync(string tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>All decision-log entries for a pool, oldest first.</summary>
    Task<IReadOnlyList<DecisionLogEntry>> GetEntriesByPoolAsync(string tenantId, string poolId, CancellationToken cancellationToken = default);
}

/// <summary>The rebuildable <see cref="Position"/> projection — upsert + query (not append-only).</summary>
public interface IPositionStore
{
    /// <summary>Inserts or replaces the position projection (rebuilt from events).</summary>
    Task UpsertAsync(Position position, CancellationToken cancellationToken = default);

    /// <summary>A position by id, or null.</summary>
    Task<Position?> GetAsync(string tenantId, string id, CancellationToken cancellationToken = default);

    /// <summary>All positions for a wallet.</summary>
    Task<IReadOnlyList<Position>> GetByWalletAsync(string tenantId, string walletId, CancellationToken cancellationToken = default);
}
