namespace FollowAlpha.LP.Domain.Signals;

/// <summary>The volatility regime of an asset — magnitude/character only, <b>never direction</b> (RN-07).</summary>
public enum Regime
{
    Range,
    Trending,
    Transition,
}

/// <summary>
/// Declared (not tuned) thresholds for <see cref="RegimeClassifier"/> (RN-14; analyst-review pending in
/// OPEN-DECISIONS.md). Windows are in bars (daily resolution today). All defaults are provisional v1
/// settings, recorded so the classification is reproducible and reviewable.
/// </summary>
public sealed record RegimePolicy(
    int RvWindow = 14,
    int TrendinessWindow = 30,
    int PercentileLookback = 90,
    int MinBars = 30,
    int PeriodsPerYear = 365,
    decimal TrendinessCutoff = 0.40m,
    decimal RangeRvPercentileMax = 60m)
{
    public static RegimePolicy Default { get; } = new();
}

/// <summary>The numeric evidence behind a regime label — always shown with the label, never the label alone (UC-02).</summary>
public sealed record RegimeEvidence(
    decimal RvPercentile,
    decimal Trendiness,
    int RvWindow,
    int PercentileLookback,
    int TrendinessWindow,
    int MinBars,
    int SampleCount,
    string ClassificationReason);

/// <summary>A regime label plus the evidence that produced it.</summary>
public sealed record RegimeResult(Regime Regime, RegimeEvidence Evidence);
