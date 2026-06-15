namespace FollowAlpha.LP.Domain.Positions;

/// <summary>
/// "What if I had split my entry capital 50/50 by value and just held it (no rebalance)." With entry
/// value <c>V0</c> at entry price <c>P0</c>, value at a later price is
/// <c>V0/2 · (1 + price/P0)</c> (token1 numeraire). A benchmark for <see cref="Intent.Harvest"/>.
/// </summary>
public readonly record struct FiftyFiftyBenchmark
{
    /// <summary>Constructs the benchmark from entry value and entry price.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="entryPrice"/> is not strictly positive.</exception>
    public FiftyFiftyBenchmark(decimal entryValue, decimal entryPrice)
    {
        if (entryPrice <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(entryPrice), entryPrice, "Entry price must be strictly positive.");
        }

        EntryValue = entryValue;
        EntryPrice = entryPrice;
    }

    /// <summary>Total value at entry (token1 numeraire).</summary>
    public decimal EntryValue { get; }

    /// <summary>The entry price (token1/token0).</summary>
    public decimal EntryPrice { get; }

    /// <summary>Builds the benchmark from the position's valuation at its entry price.</summary>
    public static FiftyFiftyBenchmark FromEntry(PositionValuation entry, decimal entryPrice) =>
        new(entry.Value, entryPrice);

    /// <summary>Value of the 50/50 hold at <paramref name="price"/> (token1 numeraire).</summary>
    public decimal ValueAt(decimal price) => EntryValue / 2m * (1m + price / EntryPrice);
}
