namespace FollowAlpha.LP.Domain.Ranges;

/// <summary>
/// The verdict plus the full snapshot that produced it (inputs + policy + the two derived metrics and
/// which gates passed). Everything needed to reproduce and audit the decision lives here — this is what
/// the decision log records.
/// </summary>
public readonly record struct RangeVerdict(
    Verdict Verdict,
    RangeVerdictInputs Inputs,
    RangeVerdictPolicy Policy,
    decimal NetExpectancy,
    decimal IvToForecastRatio,
    bool ExpectancyPositive,
    bool VolSoldRich);
