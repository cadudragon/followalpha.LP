using FollowAlpha.LP.Application.Abstractions;
using FollowAlpha.LP.Application.Persistence;
using FollowAlpha.LP.Application.Pools;

namespace FollowAlpha.LP.Application.Collection;

/// <summary>A watchlist pool to snapshot (the DataSync worker supplies these from configuration/working state).</summary>
public sealed record PoolToSnapshot(string PoolId, string ChainId, string PoolAddress);

/// <summary>Per-pool outcome of a snapshot run (for the DataSync worker's structured per-job log, NFR O2).</summary>
public sealed record PoolSnapshotOutcome(string PoolId, bool PoolSnapshotInserted, int TickRowsInserted, string? Error);

/// <summary>
/// Ingestion use case (ARCHITECTURE.md §5): for each watchlist pool, capture the pool state, the latest day
/// volume, and the full per-tick liquidity distribution as append-only facts. Idempotent by construction —
/// the snapshot timestamp (<see cref="IClock"/>) is the natural key, so re-running with the same clock
/// inserts nothing new. The tick distribution is the irrecoverable datum that justifies the always-on
/// DataSync worker. The use case never throws for one bad pool: it records the error and continues.
/// </summary>
public sealed class IngestPoolSnapshots(IPoolDataSource poolData, ISnapshotStore snapshots, IClock clock)
{
    private const string Source = "thegraph";

    public async Task<IReadOnlyList<PoolSnapshotOutcome>> RunAsync(
        IReadOnlyCollection<PoolToSnapshot> pools, CancellationToken cancellationToken = default)
    {
        var outcomes = new List<PoolSnapshotOutcome>(pools.Count);

        foreach (var pool in pools)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                outcomes.Add(await SnapshotPoolAsync(pool, cancellationToken));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                outcomes.Add(new PoolSnapshotOutcome(pool.PoolId, PoolSnapshotInserted: false, TickRowsInserted: 0, Error: ex.Message));
            }
        }

        return outcomes;
    }

    private async Task<PoolSnapshotOutcome> SnapshotPoolAsync(PoolToSnapshot pool, CancellationToken cancellationToken)
    {
        var asOf = clock.UtcNow;

        var state = await poolData.GetPoolStateAsync(pool.ChainId, pool.PoolAddress, cancellationToken);
        var dayVolumes = await poolData.GetDayVolumesAsync(pool.ChainId, pool.PoolAddress, days: 1, cancellationToken);
        var ticks = await poolData.GetTickLiquidityAsync(pool.ChainId, pool.PoolAddress, cancellationToken);

        var poolInserted = await snapshots.InsertPoolSnapshotIfAbsentAsync(
            new PoolSnapshot
            {
                PoolId = pool.PoolId,
                AsOfUtc = asOf,
                CurrentTick = state.CurrentTick,
                SqrtPriceX96 = state.SqrtPriceX96,
                Liquidity = state.Liquidity,
                Tvl = state.TvlUsd,
                DayVolumeUsd = dayVolumes.Count > 0 ? dayVolumes[0].VolumeUsd : 0m,
                Source = Source,
            },
            cancellationToken);

        var tickRows = 0;
        foreach (var tick in ticks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await snapshots.InsertTickLiquiditySnapshotIfAbsentAsync(
                    new TickLiquiditySnapshot
                    {
                        PoolId = pool.PoolId,
                        AsOfUtc = asOf,
                        Tick = tick.Tick,
                        LiquidityNet = tick.LiquidityNet,
                        LiquidityGross = tick.LiquidityGross,
                    },
                    cancellationToken))
            {
                tickRows++;
            }
        }

        return new PoolSnapshotOutcome(pool.PoolId, poolInserted, tickRows, Error: null);
    }
}
