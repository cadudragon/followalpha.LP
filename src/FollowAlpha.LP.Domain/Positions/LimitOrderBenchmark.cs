using FollowAlpha.LP.Domain.Primitives;

namespace FollowAlpha.LP.Domain.Positions;

/// <summary>Direction of the dry scaled order: accumulate (buy token0) or distribute (sell token0).</summary>
public enum LadderSide
{
    /// <summary>Spend a token1 budget to buy token0 as price falls through [a,b].</summary>
    Accumulate,

    /// <summary>Sell a token0 budget for token1 as price rises through [a,b].</summary>
    Distribute,
}

/// <summary>Shape of the scaled ladder (ARCHITECTURE.md §4.3, decided 2026-06-15).</summary>
public enum LimitLadder
{
    /// <summary>Primary: equal quote (token1) per equally-spaced price level → logarithmic-mean fill. The official report/verdict number.</summary>
    UniformQuoteByPrice,

    /// <summary>Secondary: equal base (token0) per level → arithmetic-mean fill. A sensitivity perspective only.</summary>
    UniformBaseByPrice,
}

/// <summary>
/// The honest benchmark for <see cref="Intent.Accumulate"/>/<see cref="Intent.Distribute"/>: the dry
/// (no-fee) scaled limit order you would otherwise place over the same range with the same single-sided
/// capital. Partial fills are valued at the current price — the unfilled budget stays in its original
/// token. The position's own average fill is the geometric mean √(a·b); these ladders give the
/// logarithmic mean (primary) and arithmetic mean (secondary), both ≥ geometric, so the LP buys cheaper
/// (and earns fees on top). All quantities are analytics-grade <see cref="decimal"/> in raw pool terms.
/// </summary>
public readonly record struct LimitOrderBenchmark
{
    /// <summary>Constructs a benchmark from a budget (token1 for accumulate, token0 for distribute) and range.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Budget is not strictly positive.</exception>
    /// <exception cref="ArgumentException">The range is not <c>0 &lt; lower &lt; upper</c>.</exception>
    public LimitOrderBenchmark(decimal budget, decimal lowerPrice, decimal upperPrice, LadderSide side, LimitLadder ladder)
    {
        if (budget <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(budget), budget, "Budget must be strictly positive.");
        }

        if (lowerPrice <= 0m || lowerPrice >= upperPrice)
        {
            throw new ArgumentException("Range must satisfy 0 < lowerPrice < upperPrice.", nameof(lowerPrice));
        }

        Budget = budget;
        LowerPrice = lowerPrice;
        UpperPrice = upperPrice;
        Side = side;
        Ladder = ladder;
    }

    /// <summary>Capital deployed: token1 for <see cref="LadderSide.Accumulate"/>, token0 for <see cref="LadderSide.Distribute"/>.</summary>
    public decimal Budget { get; }

    /// <summary>Lower range price a (token1/token0).</summary>
    public decimal LowerPrice { get; }

    /// <summary>Upper range price b (token1/token0).</summary>
    public decimal UpperPrice { get; }

    /// <summary>Direction of the ladder.</summary>
    public LadderSide Side { get; }

    /// <summary>Shape of the ladder.</summary>
    public LimitLadder Ladder { get; }

    /// <summary>Builds the accumulate benchmark from the position's deposited token1 over its range.</summary>
    public static LimitOrderBenchmark Accumulate(RangePosition position, LimitLadder ladder) =>
        new(position.DepositedToken1(), position.LowerPoolPrice, position.UpperPoolPrice, LadderSide.Accumulate, ladder);

    /// <summary>Builds the distribute benchmark from the position's deposited token0 over its range.</summary>
    public static LimitOrderBenchmark Distribute(RangePosition position, LimitLadder ladder) =>
        new(position.DepositedToken0(), position.LowerPoolPrice, position.UpperPoolPrice, LadderSide.Distribute, ladder);

    /// <summary>The ladder's holding at <paramref name="price"/> (token0, token1, value in token1 numeraire).</summary>
    public PositionValuation HoldingsAt(decimal price)
    {
        var a = LowerPrice;
        var b = UpperPrice;
        var pc = Math.Clamp(price, a, b);

        return Side == LadderSide.Accumulate
            ? AccumulateHoldings(a, b, pc, price)
            : DistributeHoldings(a, b, pc, price);
    }

    /// <summary>Value of the ladder at <paramref name="price"/> (token1 numeraire).</summary>
    public decimal ValueAt(decimal price) => HoldingsAt(price).Value;

    private PositionValuation AccumulateHoldings(decimal a, decimal b, decimal pc, decimal price)
    {
        // Executed rungs are [pc, b] (price fell from above b down to price); budget is token1.
        decimal token1Spent;
        decimal token0;
        if (Ladder == LimitLadder.UniformQuoteByPrice)
        {
            token1Spent = Budget * (b - pc) / (b - a);
            token0 = Budget / (b - a) * PriceMath.Ln(b / pc);
        }
        else
        {
            token1Spent = Budget * (b * b - pc * pc) / (b * b - a * a);
            token0 = 2m * Budget * (b - pc) / (b * b - a * a);
        }

        var token1Remaining = Budget - token1Spent;
        return new PositionValuation(token0, token1Remaining, token1Remaining + token0 * price);
    }

    private PositionValuation DistributeHoldings(decimal a, decimal b, decimal pc, decimal price)
    {
        // Executed rungs are [a, pc] (price rose from below a up to price); budget is token0.
        decimal token0Sold;
        decimal token1Received;
        if (Ladder == LimitLadder.UniformQuoteByPrice)
        {
            var r = Budget / PriceMath.Ln(b / a);
            token0Sold = r * PriceMath.Ln(pc / a);
            token1Received = r * (pc - a);
        }
        else
        {
            var q = Budget / (b - a);
            token0Sold = q * (pc - a);
            token1Received = q * (pc * pc - a * a) / 2m;
        }

        var token0Remaining = Budget - token0Sold;
        return new PositionValuation(token0Remaining, token1Received, token1Received + token0Remaining * price);
    }
}
