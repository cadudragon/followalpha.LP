using FollowAlpha.LP.Application.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FollowAlpha.LP.Infrastructure.Persistence;

// EF Core implementations of the persistence ports. Idempotent insert-if-absent is a provider-agnostic
// query-then-add on the natural key (single-writer Collector; no provider-specific UPSERT, per PT2).
// Append-only stores expose no update/delete; Position is the rebuildable projection (upsert).

/// <summary>Append-only price-bar store.</summary>
public sealed class EfPriceStore(AppDbContext db) : IPriceStore
{
    public async Task<bool> InsertIfAbsentAsync(PriceBar bar, CancellationToken cancellationToken = default)
    {
        var exists = await db.PriceBars.AnyAsync(
            x => x.TenantId == bar.TenantId && x.AssetId == bar.AssetId && x.Resolution == bar.Resolution && x.OpenTimeUtc == bar.OpenTimeUtc,
            cancellationToken);
        if (exists)
        {
            return false;
        }

        db.PriceBars.Add(bar);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<PriceBar>> GetByAssetAsync(string tenantId, string assetId, string resolution, CancellationToken cancellationToken = default) =>
        await db.PriceBars
            .Where(x => x.TenantId == tenantId && x.AssetId == assetId && x.Resolution == resolution)
            .OrderBy(x => x.OpenTimeUtc)
            .ToListAsync(cancellationToken);
}

/// <summary>Append-only pool/tick snapshot store.</summary>
public sealed class EfSnapshotStore(AppDbContext db) : ISnapshotStore
{
    public async Task<bool> InsertPoolSnapshotIfAbsentAsync(PoolSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var exists = await db.PoolSnapshots.AnyAsync(
            x => x.TenantId == snapshot.TenantId && x.PoolId == snapshot.PoolId && x.AsOfUtc == snapshot.AsOfUtc,
            cancellationToken);
        if (exists)
        {
            return false;
        }

        db.PoolSnapshots.Add(snapshot);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> InsertTickLiquiditySnapshotIfAbsentAsync(TickLiquiditySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var exists = await db.TickLiquiditySnapshots.AnyAsync(
            x => x.TenantId == snapshot.TenantId && x.PoolId == snapshot.PoolId && x.AsOfUtc == snapshot.AsOfUtc && x.Tick == snapshot.Tick,
            cancellationToken);
        if (exists)
        {
            return false;
        }

        db.TickLiquiditySnapshots.Add(snapshot);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<PoolSnapshot?> GetLatestPoolSnapshotAsync(string tenantId, string poolId, CancellationToken cancellationToken = default) =>
        await db.PoolSnapshots
            .Where(x => x.TenantId == tenantId && x.PoolId == poolId)
            .OrderByDescending(x => x.AsOfUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TickLiquiditySnapshot>> GetTickLiquidityAsync(string tenantId, string poolId, DateTimeOffset asOfUtc, CancellationToken cancellationToken = default) =>
        await db.TickLiquiditySnapshots
            .Where(x => x.TenantId == tenantId && x.PoolId == poolId && x.AsOfUtc == asOfUtc)
            .OrderBy(x => x.Tick)
            .ToListAsync(cancellationToken);
}

/// <summary>Append-only position-event store.</summary>
public sealed class EfPositionEventStore(AppDbContext db) : IPositionEventStore
{
    public async Task<bool> InsertIfAbsentAsync(PositionEvent positionEvent, CancellationToken cancellationToken = default)
    {
        var exists = await db.PositionEvents.AnyAsync(
            x => x.TenantId == positionEvent.TenantId && x.ChainId == positionEvent.ChainId && x.TxHash == positionEvent.TxHash && x.LogIndex == positionEvent.LogIndex,
            cancellationToken);
        if (exists)
        {
            return false;
        }

        db.PositionEvents.Add(positionEvent);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<PositionEvent>> GetByWalletAsync(string tenantId, string walletId, CancellationToken cancellationToken = default) =>
        await db.PositionEvents
            .Where(x => x.TenantId == tenantId && x.WalletId == walletId)
            .OrderBy(x => x.BlockTimeUtc)
            .ToListAsync(cancellationToken);
}

/// <summary>Append-only intent-history store.</summary>
public sealed class EfIntentRecordStore(AppDbContext db) : IIntentRecordStore
{
    public async Task AppendAsync(IntentRecord record, CancellationToken cancellationToken = default)
    {
        if (record.Id == Guid.Empty)
        {
            throw new ArgumentException("IntentRecord.Id must be supplied by the caller.", nameof(record));
        }

        db.IntentRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IntentRecord>> GetByPositionAsync(string tenantId, string positionId, CancellationToken cancellationToken = default) =>
        await db.IntentRecords
            .Where(x => x.TenantId == tenantId && x.PositionId == positionId)
            .OrderBy(x => x.DeclaredAtUtc)
            .ToListAsync(cancellationToken);
}

/// <summary>Append-only decision log.</summary>
public sealed class EfDecisionLog(AppDbContext db) : IDecisionLog
{
    public async Task AppendEntryAsync(DecisionLogEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry.Id == Guid.Empty)
        {
            throw new ArgumentException("DecisionLogEntry.Id must be supplied by the caller.", nameof(entry));
        }

        db.DecisionLogEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AppendAnnotationAsync(DecisionAnnotation annotation, CancellationToken cancellationToken = default)
    {
        if (annotation.Id == Guid.Empty)
        {
            throw new ArgumentException("DecisionAnnotation.Id must be supplied by the caller.", nameof(annotation));
        }

        db.DecisionAnnotations.Add(annotation);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<DecisionLogEntry?> GetEntryAsync(string tenantId, Guid id, CancellationToken cancellationToken = default) =>
        await db.DecisionLogEntries.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<DecisionLogEntry>> GetEntriesByPoolAsync(string tenantId, string poolId, CancellationToken cancellationToken = default) =>
        await db.DecisionLogEntries
            .Where(x => x.TenantId == tenantId && x.PoolId == poolId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
}

/// <summary>The rebuildable position projection (upsert).</summary>
public sealed class EfPositionStore(AppDbContext db) : IPositionStore
{
    public async Task UpsertAsync(Position position, CancellationToken cancellationToken = default)
    {
        var existing = await db.Positions.FirstOrDefaultAsync(x => x.Id == position.Id, cancellationToken);
        if (existing is null)
        {
            db.Positions.Add(position);
        }
        else
        {
            db.Entry(existing).CurrentValues.SetValues(position);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Position?> GetAsync(string tenantId, string id, CancellationToken cancellationToken = default) =>
        await db.Positions.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Position>> GetByWalletAsync(string tenantId, string walletId, CancellationToken cancellationToken = default) =>
        await db.Positions
            .Where(x => x.TenantId == tenantId && x.WalletId == walletId)
            .ToListAsync(cancellationToken);
}
