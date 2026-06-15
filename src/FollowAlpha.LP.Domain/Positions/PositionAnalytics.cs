using FollowAlpha.LP.Domain.Primitives;

namespace FollowAlpha.LP.Domain.Positions;

/// <summary>
/// Pure point/path analytics over a position and its benchmarks (ARCHITECTURE.md §4.3). All values are
/// token1-numeraire analytics-grade <see cref="decimal"/>; costs and fees arrive as inputs (the Domain
/// performs no I/O and reads no chain).
/// </summary>
public static class PositionAnalytics
{
    /// <summary>
    /// Loss versus a benchmark: <c>benchmarkValue − positionValue</c>. Positive means the benchmark beat
    /// the position (the position underperformed the alternative you would have taken); negative means
    /// the position is ahead on principal (before fees).
    /// </summary>
    public static decimal ImpermanentLoss(decimal positionValue, decimal benchmarkValue) =>
        benchmarkValue - positionValue;

    /// <summary>
    /// Net position result after crediting fees earned and charging the exit cost (gas + slippage),
    /// all supplied in the token1 numeraire.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Fees or exit cost is negative.</exception>
    public static decimal NetValueAfterCosts(decimal positionValue, decimal feesEarned, decimal exitCost)
    {
        if (feesEarned < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(feesEarned), feesEarned, "Fees earned cannot be negative.");
        }

        if (exitCost < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(exitCost), exitCost, "Exit cost cannot be negative.");
        }

        return positionValue + feesEarned - exitCost;
    }

    /// <summary>The position's valuation at each price along a path (e.g. a historical or scenario series).</summary>
    public static IReadOnlyList<PositionValuation> ValueAlongPath(RangePosition position, IEnumerable<PoolPrice> path)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(path);

        var series = new List<PositionValuation>();
        foreach (var price in path)
        {
            series.Add(position.ValueAt(price));
        }

        return series;
    }
}
