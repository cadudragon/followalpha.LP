using FollowAlpha.LP.Application.Exploration;
using FollowAlpha.LP.Application.Persistence;

namespace FollowAlpha.LP.Application.Tests.Exploration;

/// <summary>In-memory <see cref="IExplorationReadStore"/> for the exploration use-case tests.</summary>
internal sealed class FakeExplorationReadStore : IExplorationReadStore
{
    public List<Asset> Assets { get; } = [];

    public List<Pool> Pools { get; } = [];

    public Task<IReadOnlyList<Asset>> GetWatchlistAssetsAsync(string tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Asset>>([.. Assets.Where(a => a.TenantId == tenantId && a.InWatchlist)]);

    public Task<Asset?> GetAssetAsync(string tenantId, string assetId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Assets.FirstOrDefault(a => a.TenantId == tenantId && a.Id == assetId));

    public Task<IReadOnlyList<Pool>> GetPoolsForAssetAsync(string tenantId, string assetId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Pool>>([.. Pools.Where(p => p.TenantId == tenantId && (p.Token0AssetId == assetId || p.Token1AssetId == assetId))]);

    public Task<Pool?> GetPoolAsync(string tenantId, string poolId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Pools.FirstOrDefault(p => p.TenantId == tenantId && p.Id == poolId));

    public Task<IReadOnlyDictionary<string, string>> GetAssetSymbolsAsync(string tenantId, IEnumerable<string> assetIds, CancellationToken cancellationToken = default)
    {
        var ids = assetIds.Distinct().ToHashSet();
        return Task.FromResult<IReadOnlyDictionary<string, string>>(
            Assets.Where(a => a.TenantId == tenantId && ids.Contains(a.Id)).ToDictionary(a => a.Id, a => a.Symbol));
    }
}
