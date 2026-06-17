using FollowAlpha.LP.Application.Persistence;

namespace FollowAlpha.LP.Application.Exploration;

/// <summary>
/// Read-only access to the working-state catalogue (assets + pools) for the asset/pool exploration use
/// cases (UC-02). Facts (price bars, snapshots, tick liquidity) are read through the existing append-only
/// stores (<c>IPriceStore</c>, <c>ISnapshotStore</c>); this port only covers the catalogue rows the API
/// needs to navigate. No writes — exploration is read-mostly (API-CONTRACT §1).
/// </summary>
public interface IExplorationReadStore
{
    /// <summary>Watchlist assets (those flagged <c>InWatchlist</c>).</summary>
    Task<IReadOnlyList<Asset>> GetWatchlistAssetsAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>A single asset by its chain-aware id, or null if unknown.</summary>
    Task<Asset?> GetAssetAsync(string tenantId, string assetId, CancellationToken cancellationToken = default);

    /// <summary>The pools that reference the asset as token0 or token1.</summary>
    Task<IReadOnlyList<Pool>> GetPoolsForAssetAsync(string tenantId, string assetId, CancellationToken cancellationToken = default);

    /// <summary>A single pool by id, or null if unknown.</summary>
    Task<Pool?> GetPoolAsync(string tenantId, string poolId, CancellationToken cancellationToken = default);

    /// <summary>Symbols for the given asset ids (id → symbol), for building pool pair labels.</summary>
    Task<IReadOnlyDictionary<string, string>> GetAssetSymbolsAsync(string tenantId, IEnumerable<string> assetIds, CancellationToken cancellationToken = default);
}
