namespace FollowAlpha.LP.Domain.Ranges;

/// <summary>
/// The explicit decision thresholds for <see cref="RangeVerdictCalculator"/>. Thresholds live here as
/// caller-supplied policy — never hardcoded magic in the calculator — so they are declared before
/// results and never tuned to outcomes (LP-KNOWLEDGE.md §6.1). The policy is recorded alongside the
/// verdict in the decision log.
/// </summary>
public readonly record struct RangeVerdictPolicy
{
    /// <summary>Constructs a verdict policy.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minIvToForecastRatio"/> is not strictly positive.</exception>
    public RangeVerdictPolicy(decimal minIvToForecastRatio, decimal minNetExpectancy)
    {
        if (minIvToForecastRatio <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(minIvToForecastRatio), minIvToForecastRatio, "Minimum IV/forecast ratio must be strictly positive.");
        }

        MinIvToForecastRatio = minIvToForecastRatio;
        MinNetExpectancy = minNetExpectancy;
    }

    /// <summary>
    /// The minimum pool-IV ÷ forecast-vol ratio to consider vol "sold rich" (e.g. 1.0 = IV must at least
    /// equal the forecast). Below this the verdict is DON'T OPEN regardless of APR (LP-KNOWLEDGE.md §6b).
    /// </summary>
    public decimal MinIvToForecastRatio { get; }

    /// <summary>The minimum net expectancy (expected fees − expected exit cost) to consider opening (e.g. 0).</summary>
    public decimal MinNetExpectancy { get; }
}
