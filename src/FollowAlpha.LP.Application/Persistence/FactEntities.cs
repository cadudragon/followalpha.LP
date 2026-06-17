namespace FollowAlpha.LP.Application.Persistence;

// Fact aggregates (DATA-MODEL.md §2 — append-only, idempotent re-ingestion via natural keys). Raw
// on-chain integers (sqrtPriceX96, liquidity) are stored as text to avoid precision loss; human-scale
// values are decimal. The natural key of each fact is its composite primary key (scoped by TenantId).

/// <summary>Spot OHLCV bar. Natural key: (TenantId, AssetId, Resolution, OpenTimeUtc).</summary>
public sealed class PriceBar
{
    public string TenantId { get; set; } = Tenancy.DefaultTenantId;
    public required string AssetId { get; set; }
    public required string Resolution { get; set; }
    public DateTimeOffset OpenTimeUtc { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
    public required string Source { get; set; }
}

/// <summary>Pool state at a moment. Natural key: (TenantId, PoolId, AsOfUtc).</summary>
public sealed class PoolSnapshot
{
    public string TenantId { get; set; } = Tenancy.DefaultTenantId;
    public required string PoolId { get; set; }
    public DateTimeOffset AsOfUtc { get; set; }
    public int CurrentTick { get; set; }
    /// <summary>Raw Q64.96 sqrt price (text).</summary>
    public required string SqrtPriceX96 { get; set; }
    /// <summary>Raw active liquidity L (text).</summary>
    public required string Liquidity { get; set; }
    public decimal Tvl { get; set; }
    public decimal DayVolumeUsd { get; set; }
    public required string Source { get; set; }
}

/// <summary>
/// Per-tick liquidity distribution at a moment — the datum that cannot be reconstructed retroactively
/// (drives the always-on Collector). Natural key: (TenantId, PoolId, AsOfUtc, Tick).
/// </summary>
public sealed class TickLiquiditySnapshot
{
    public string TenantId { get; set; } = Tenancy.DefaultTenantId;
    public required string PoolId { get; set; }
    public DateTimeOffset AsOfUtc { get; set; }
    public int Tick { get; set; }
    /// <summary>Raw net liquidity at the tick (text).</summary>
    public required string LiquidityNet { get; set; }
    /// <summary>Raw gross liquidity at the tick (text).</summary>
    public required string LiquidityGross { get; set; }
}

/// <summary>
/// An on-chain position event (mint/burn/collect) — the audit source of truth. Natural key:
/// (TenantId, ChainId, TxHash, LogIndex).
/// </summary>
public sealed class PositionEvent
{
    public string TenantId { get; set; } = Tenancy.DefaultTenantId;
    public required string ChainId { get; set; }
    public required string TxHash { get; set; }
    public int LogIndex { get; set; }
    public required string WalletId { get; set; }
    public required string PoolId { get; set; }
    public required string EventType { get; set; }
    public int TickLower { get; set; }
    public int TickUpper { get; set; }
    /// <summary>Raw signed liquidity delta (text).</summary>
    public required string LiquidityDelta { get; set; }
    public decimal Amount0 { get; set; }
    public decimal Amount1 { get; set; }
    public decimal FeesCollected0 { get; set; }
    public decimal FeesCollected1 { get; set; }

    // Native gas is persisted raw now (the irreversible on-chain fact); USD conversion is deferred until a
    // reliable historical price source exists (decided 2026-06-17). Never zero/current-price/guess.
    /// <summary>Raw gas units used by the tx (text).</summary>
    public required string GasUsed { get; set; }
    /// <summary>Raw effective gas price in wei (text); null when the receipt did not report it.</summary>
    public string? EffectiveGasPriceWei { get; set; }
    /// <summary>Raw native gas cost in wei = GasUsed × EffectiveGasPrice (text); null when price is unknown.</summary>
    public string? NativeGasCostWei { get; set; }
    /// <summary>Gas cost in USD — derived, populated only once a historical price source lands (deferred).</summary>
    public decimal? GasCostUsd { get; set; }

    public DateTimeOffset BlockTimeUtc { get; set; }
}
