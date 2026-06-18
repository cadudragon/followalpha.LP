namespace FollowAlpha.LP.Application.Pools;

/// <summary>
/// Current pool state from the data source. Raw on-chain integers (<see cref="SqrtPriceX96"/>,
/// <see cref="Liquidity"/>) are kept as text to avoid precision loss; the DataSync worker maps these into a
/// <c>PoolSnapshot</c> fact.
/// </summary>
public sealed record PoolState(
    string PoolAddress,
    int CurrentTick,
    string SqrtPriceX96,
    string Liquidity,
    int FeeTier,
    decimal TvlUsd);

/// <summary>One day of pool volume (UTC day → USD volume).</summary>
public sealed record PoolDayVolume(DateTimeOffset Date, decimal VolumeUsd);

/// <summary>Net/gross liquidity at one initialized tick. Raw integers kept as text.</summary>
public sealed record TickLiquidity(int Tick, string LiquidityNet, string LiquidityGross);
