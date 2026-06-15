namespace FollowAlpha.LP.Domain.Positions;

/// <summary>
/// "What if I had just held my initial tokens." Built from the position's holding at entry
/// (<paramref name="AmountX0"/> token0, <paramref name="AmountY0"/> token1); value at a later price is
/// <c>AmountY0 + AmountX0·price</c> (token1 numeraire). The benchmark for <see cref="Intent.Harvest"/>.
/// </summary>
public readonly record struct HodlBenchmark(decimal AmountX0, decimal AmountY0)
{
    /// <summary>Builds the benchmark from the position's valuation at its entry price.</summary>
    public static HodlBenchmark FromEntry(PositionValuation entry) => new(entry.AmountX, entry.AmountY);

    /// <summary>Value of the held tokens at <paramref name="price"/> (token1 numeraire).</summary>
    public decimal ValueAt(decimal price) => AmountY0 + AmountX0 * price;
}
