using FollowAlpha.LP.Application.Exploration;
using FollowAlpha.LP.Application.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FollowAlpha.LP.Infrastructure.Persistence;

/// <summary>
/// EF Core read-only adapter for the asset/pool catalogue (UC-02 exploration). Read-only and untracked —
/// the API never mutates working state through this port. Facts (price bars, snapshots, ticks) come through
/// the existing append-only stores, not here.
/// </summary>
public sealed class EfExplorationReadStore(AppDbContext db) : IExplorationReadStore
{
    public async Task<IReadOnlyList<Asset>> GetWatchlistAssetsAsync(string tenantId, CancellationToken cancellationToken = default) =>
        await db.Assets.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.InWatchlist)
            .OrderBy(a => a.Symbol).ThenBy(a => a.Id)
            .ToListAsync(cancellationToken);

    public async Task<Asset?> GetAssetAsync(string tenantId, string assetId, CancellationToken cancellationToken = default) =>
        await db.Assets.AsNoTracking()
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == assetId, cancellationToken);

    public async Task<IReadOnlyList<Pool>> GetPoolsForAssetAsync(string tenantId, string assetId, CancellationToken cancellationToken = default) =>
        await db.Pools.AsNoTracking()
            .Where(p => p.TenantId == tenantId && (p.Token0AssetId == assetId || p.Token1AssetId == assetId))
            .OrderBy(p => p.FeeTier).ThenBy(p => p.Id)
            .ToListAsync(cancellationToken);

    public async Task<Pool?> GetPoolAsync(string tenantId, string poolId, CancellationToken cancellationToken = default) =>
        await db.Pools.AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == poolId, cancellationToken);

    public async Task<IReadOnlyDictionary<string, string>> GetAssetSymbolsAsync(string tenantId, IEnumerable<string> assetIds, CancellationToken cancellationToken = default)
    {
        var ids = assetIds.Distinct().ToList();
        return await db.Assets.AsNoTracking()
            .Where(a => a.TenantId == tenantId && ids.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Symbol, cancellationToken);
    }
}
