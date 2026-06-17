using FollowAlpha.LP.Application.Abstractions;
using FollowAlpha.LP.Application.Errors;
using FollowAlpha.LP.Application.Persistence;

namespace FollowAlpha.LP.Application.Exploration;

/// <summary>UC-02 `/assets/{id}/pools`: the asset's pools with fee tier, volume/TVL, pool IV and competing liquidity. Null = unknown asset (404).</summary>
public sealed class ListAssetPools(IExplorationReadStore reads, ISnapshotStore snapshots, IClock clock, ExplorationPolicy policy)
{
    public async Task<IReadOnlyList<PoolComparisonRow>?> RunAsync(string assetId, CancellationToken cancellationToken = default)
    {
        var tenant = Tenancy.DefaultTenantId;
        if (await reads.GetAssetAsync(tenant, assetId, cancellationToken) is null)
        {
            return null;
        }

        var pools = await reads.GetPoolsForAssetAsync(tenant, assetId, cancellationToken);
        var symbols = await reads.GetAssetSymbolsAsync(
            tenant, pools.SelectMany(p => new[] { p.Token0AssetId, p.Token1AssetId }).Distinct(), cancellationToken);

        var rows = new List<PoolComparisonRow>(pools.Count);
        foreach (var pool in pools)
        {
            rows.Add(await BuildRowAsync(tenant, pool, symbols, cancellationToken));
        }

        return rows;
    }

    private async Task<PoolComparisonRow> BuildRowAsync(
        string tenant, Pool pool, IReadOnlyDictionary<string, string> symbols, CancellationToken cancellationToken)
    {
        var pair = ExplorationMetrics.Pair(pool, symbols);
        var snap = await snapshots.GetLatestPoolSnapshotAsync(tenant, pool.Id, cancellationToken);

        // A stale or absent snapshot is never shown as a current signal: flag the row, null the derived metrics.
        if (snap is null)
        {
            return Row(pool, pair, asOf: null, ExplorationWire.DataStatus.NoSnapshot, volumeUsd: null, tvlUsd: null,
                volTvlRatio: null, ExplorationMetrics.NoIv(null), ExplorationMetrics.NoCompeting(policy.CompetingLiquidityBandPct, null));
        }

        if (clock.UtcNow - snap.AsOfUtc > policy.SnapshotStaleAfter)
        {
            return Row(pool, pair, snap.AsOfUtc, ExplorationWire.DataStatus.Stale, volumeUsd: null, tvlUsd: null,
                volTvlRatio: null, ExplorationMetrics.NoIv(snap.AsOfUtc), ExplorationMetrics.NoCompeting(policy.CompetingLiquidityBandPct, snap.AsOfUtc));
        }

        var ticks = await snapshots.GetTickLiquidityAsync(tenant, pool.Id, snap.AsOfUtc, cancellationToken);
        var volTvlRatio = snap.Tvl > 0m ? snap.DayVolumeUsd / snap.Tvl : (decimal?)null;
        return Row(pool, pair, snap.AsOfUtc, ExplorationWire.DataStatus.Ok,
            ExplorationMetrics.Money(snap.DayVolumeUsd), ExplorationMetrics.Money(snap.Tvl), volTvlRatio,
            ExplorationMetrics.Iv(pool, snap), ExplorationMetrics.Competing(pool, snap, ticks, policy.CompetingLiquidityBandPct));
    }

    private static PoolComparisonRow Row(
        Pool pool, string pair, DateTimeOffset? asOf, string dataStatus, string? volumeUsd, string? tvlUsd,
        decimal? volTvlRatio, PoolIvDto iv, CompetingLiquidityDto competing) =>
        new(pool.Id, pair, pool.ChainId, pool.FeeTier, asOf, dataStatus, volumeUsd, tvlUsd, volTvlRatio, iv, competing);
}

/// <summary>UC-02 `/pools/{poolId}`: pool detail with the latest snapshot, IV/competing-liquidity, and tick distribution. Null = unknown pool (404); 422 if no/stale snapshot.</summary>
public sealed class GetPoolDetail(IExplorationReadStore reads, ISnapshotStore snapshots, IClock clock, ExplorationPolicy policy)
{
    public async Task<PoolDetail?> RunAsync(string poolId, CancellationToken cancellationToken = default)
    {
        var tenant = Tenancy.DefaultTenantId;
        var pool = await reads.GetPoolAsync(tenant, poolId, cancellationToken);
        if (pool is null)
        {
            return null;
        }

        var snap = await snapshots.GetLatestPoolSnapshotAsync(tenant, poolId, cancellationToken);
        if (snap is null)
        {
            throw new InsufficientDataException("This pool has no snapshot yet.", ["poolSnapshot"]);
        }

        if (clock.UtcNow - snap.AsOfUtc > policy.SnapshotStaleAfter)
        {
            throw new InsufficientDataException(
                $"Latest snapshot ({snap.AsOfUtc:O}) is stale beyond {policy.SnapshotStaleAfter}; not a current signal.", ["freshPoolSnapshot"]);
        }

        var ticks = await snapshots.GetTickLiquidityAsync(tenant, poolId, snap.AsOfUtc, cancellationToken);
        var symbols = await reads.GetAssetSymbolsAsync(tenant, [pool.Token0AssetId, pool.Token1AssetId], cancellationToken);

        var snapshotDto = new PoolSnapshotDto(
            snap.AsOfUtc, snap.CurrentTick, snap.SqrtPriceX96, snap.Liquidity,
            ExplorationMetrics.Money(snap.Tvl), ExplorationMetrics.Money(snap.DayVolumeUsd), snap.Source);
        var ticksDto = ticks.Select(t => new TickLiquidityDto(t.Tick, t.LiquidityNet, t.LiquidityGross)).ToList();
        var volTvlRatio = snap.Tvl > 0m ? snap.DayVolumeUsd / snap.Tvl : (decimal?)null;

        return new PoolDetail(
            pool.Id, ExplorationMetrics.Pair(pool, symbols), pool.ChainId, pool.FeeTier, pool.TickSpacing,
            snapshotDto, volTvlRatio, ExplorationMetrics.Iv(pool, snap),
            ExplorationMetrics.Competing(pool, snap, ticks, policy.CompetingLiquidityBandPct), ticksDto);
    }
}
