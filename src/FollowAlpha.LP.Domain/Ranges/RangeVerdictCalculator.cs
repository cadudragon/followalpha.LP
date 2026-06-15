namespace FollowAlpha.LP.Domain.Ranges;

/// <summary>
/// Combines the §4.4 signal outputs into an OPEN / DON'T OPEN verdict (ARCHITECTURE.md §4.4;
/// LP-KNOWLEDGE.md §5/§6b). Two conditions are <b>both</b> necessary: net expectancy (expected fees over
/// the likely horizon minus expected exit cost) must clear the policy floor, and the pool must be selling
/// vol rich (IV ÷ forecast ≥ the policy ratio) — the IV gate is a veto "regardless of the advertised APR"
/// (LP-KNOWLEDGE.md §6b). The rule is a transparent gate; all thresholds come from
/// <see cref="RangeVerdictPolicy"/> (no magic constants, no tuning to results). Pure and deterministic.
/// </summary>
public static class RangeVerdictCalculator
{
    /// <summary>Evaluates the verdict for the given input snapshot under the given policy.</summary>
    public static RangeVerdict Evaluate(RangeVerdictInputs inputs, RangeVerdictPolicy policy)
    {
        var netExpectancy = inputs.ExpectedFeesOverHorizon - inputs.ExpectedExitCost;
        var ivToForecastRatio = inputs.PoolImpliedVol / inputs.ForecastVol;

        var expectancyPositive = netExpectancy >= policy.MinNetExpectancy;
        var volSoldRich = ivToForecastRatio >= policy.MinIvToForecastRatio;

        var verdict = expectancyPositive && volSoldRich ? Verdict.Open : Verdict.DoNotOpen;

        return new RangeVerdict(
            verdict,
            inputs,
            policy,
            netExpectancy,
            ivToForecastRatio,
            expectancyPositive,
            volSoldRich);
    }
}
