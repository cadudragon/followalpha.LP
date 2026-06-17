namespace FollowAlpha.LP.Application.Prices;

/// <summary>
/// Reads a token's daily USD OHLCV history (ARCHITECTURE.md §5/§6; The Graph <c>tokenDayData</c> in v1).
/// Read-only; the chain id resolves the protocol/subgraph via the registry. This is the raw material the
/// Range Advisor's regime/RV needs in Phase 3 — Chainlink stays a future cross-check (decided 2026-06-17).
/// Per-asset USD bars; the pair price for an analysis derives from <c>token1USD/token0USD</c>.
/// </summary>
public interface IPriceSeriesSource
{
    /// <summary>The most recent <paramref name="days"/> of daily USD OHLCV for a token, ascending by day.</summary>
    Task<IReadOnlyList<AssetUsdBar>> GetDailyUsdBarsAsync(
        string chainId, string tokenAddress, int days, CancellationToken cancellationToken = default);
}

/// <summary>One day of USD OHLCV for a token (the source-shaped value the ingestion maps to a <c>PriceBar</c>).</summary>
public sealed record AssetUsdBar(
    DateTimeOffset DayStartUtc,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal VolumeUsd);
