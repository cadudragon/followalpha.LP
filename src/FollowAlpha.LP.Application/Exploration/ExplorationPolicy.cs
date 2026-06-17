using FollowAlpha.LP.Domain.Signals;

namespace FollowAlpha.LP.Application.Exploration;

/// <summary>
/// Declared (not tuned) parameters for asset/pool exploration (analyst-review pending, OPEN-DECISIONS.md).
/// Staleness bounds keep a stale snapshot/bar from masquerading as a current signal (RN-08): single-resource
/// endpoints raise the 422 path, list rows are flagged. All defaults are provisional v1 settings.
/// </summary>
public sealed record ExplorationPolicy(
    RegimePolicy Regime,
    decimal CompetingLiquidityBandPct,
    TimeSpan SnapshotStaleAfter,
    TimeSpan PriceBarStaleAfter,
    string PriceResolution = "1d",
    int PeriodsPerYear = 365)
{
    /// <summary>Realized-vol summary windows (in bars) reported on `/assets` and in `rvVsPoolIv`.</summary>
    public int RvSummaryShort { get; init; } = 7;
    public int RvSummaryMedium { get; init; } = 30;
    public int RvSummaryLong { get; init; } = 90;

    public static ExplorationPolicy Default { get; } = new(
        Regime: RegimePolicy.Default,
        CompetingLiquidityBandPct: 2.0m,
        // Default 2× the collector's pool-snapshot cadence (1h) and price cadence (daily) — API-CONTRACT staleness.
        SnapshotStaleAfter: TimeSpan.FromHours(2),
        PriceBarStaleAfter: TimeSpan.FromDays(2));
}
