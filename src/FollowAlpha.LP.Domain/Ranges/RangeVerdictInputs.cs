namespace FollowAlpha.LP.Domain.Ranges;

/// <summary>
/// The quantitative inputs the verdict is computed from — the snapshot recorded in the decision log so
/// the verdict is reproducible. These are the combined outputs of the §4.4 estimators (computed by the
/// caller): the pool's implied vol vs the forecast realized vol, and expected fees over the likely
/// horizon vs the expected exit cost (LP-KNOWLEDGE.md §2 #4 and §6b). All in the token1 numeraire /
/// annualized fractions, analytics-grade <see cref="decimal"/>.
/// </summary>
public readonly record struct RangeVerdictInputs
{
    /// <summary>Constructs the verdict input snapshot.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A vol/fee/cost is negative, or forecast vol is not strictly positive.</exception>
    public RangeVerdictInputs(
        decimal poolImpliedVol,
        decimal forecastVol,
        decimal expectedFeesOverHorizon,
        decimal expectedExitCost)
    {
        if (poolImpliedVol < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(poolImpliedVol), poolImpliedVol, "Implied vol cannot be negative.");
        }

        if (forecastVol <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(forecastVol), forecastVol, "Forecast vol must be strictly positive.");
        }

        if (expectedFeesOverHorizon < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedFeesOverHorizon), expectedFeesOverHorizon, "Expected fees cannot be negative.");
        }

        if (expectedExitCost < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedExitCost), expectedExitCost, "Expected exit cost cannot be negative.");
        }

        PoolImpliedVol = poolImpliedVol;
        ForecastVol = forecastVol;
        ExpectedFeesOverHorizon = expectedFeesOverHorizon;
        ExpectedExitCost = expectedExitCost;
    }

    /// <summary>The pool's implied volatility (annualized fraction; see <c>ImpliedVolCalculator</c>).</summary>
    public decimal PoolImpliedVol { get; }

    /// <summary>The forecast realized volatility to compare against (annualized fraction).</summary>
    public decimal ForecastVol { get; }

    /// <summary>Expected fees over the likely in-range horizon (token1 numeraire).</summary>
    public decimal ExpectedFeesOverHorizon { get; }

    /// <summary>Expected cost to exit at the likely horizon — IL + gas/slippage (token1 numeraire).</summary>
    public decimal ExpectedExitCost { get; }
}
