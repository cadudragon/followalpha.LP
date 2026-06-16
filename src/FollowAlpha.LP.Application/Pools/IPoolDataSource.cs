namespace FollowAlpha.LP.Application.Pools;

/// <summary>
/// Reads pool state, day volume, and the tick-liquidity distribution for a pool (ARCHITECTURE.md §5/§6;
/// The Graph in v1). Read-only; the chain id resolves the protocol descriptor via the registry. The
/// tick distribution is the irrecoverable datum that drives the always-on Collector.
/// </summary>
public interface IPoolDataSource
{
    /// <summary>Current pool state (tick, sqrt price, liquidity, fee tier, TVL).</summary>
    Task<PoolState> GetPoolStateAsync(string chainId, string poolAddress, CancellationToken cancellationToken = default);

    /// <summary>The most recent <paramref name="days"/> of daily volume, newest first.</summary>
    Task<IReadOnlyList<PoolDayVolume>> GetDayVolumesAsync(string chainId, string poolAddress, int days, CancellationToken cancellationToken = default);

    /// <summary>The full per-tick liquidity distribution (paginated internally), ascending by tick.</summary>
    Task<IReadOnlyList<TickLiquidity>> GetTickLiquidityAsync(string chainId, string poolAddress, CancellationToken cancellationToken = default);
}
