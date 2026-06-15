namespace FollowAlpha.LP.Domain.Primitives;

/// <summary>
/// A requested price band in <see cref="HumanPrice"/> terms, both bounds in the same orientation.
/// <see cref="ToInitializedTicks"/> scales it to raw pool prices with the pool's decimals and maps it
/// to initialized ticks for a fee tier — a concern kept separate from the canonical math. It rounds
/// <i>outward</i> (lower down, upper up) so the returned tick band always <b>contains</b> the request
/// and never silently narrows it (ARCHITECTURE.md §4.1).
/// </summary>
public readonly record struct PriceRange
{
    /// <summary>Constructs a range; bounds must share an orientation and be strictly ordered.</summary>
    /// <exception cref="ArgumentException">Bounds have different orientations, or <paramref name="lower"/> is not below <paramref name="upper"/>.</exception>
    public PriceRange(HumanPrice lower, HumanPrice upper)
    {
        if (lower.Orientation != upper.Orientation)
        {
            throw new ArgumentException("Range bounds must share the same orientation.", nameof(upper));
        }

        if (lower.Value >= upper.Value)
        {
            throw new ArgumentException("Lower bound must be strictly below the upper bound.", nameof(lower));
        }

        Lower = lower;
        Upper = upper;
    }

    /// <summary>The lower price bound (in <see cref="HumanPrice.Orientation"/>).</summary>
    public HumanPrice Lower { get; }

    /// <summary>The upper price bound (in <see cref="HumanPrice.Orientation"/>).</summary>
    public HumanPrice Upper { get; }

    /// <summary>
    /// The initialized tick band, in canonical (raw token1/token0) terms, that contains this range for
    /// <paramref name="feeTier"/> given the pool's <paramref name="decimals"/>. Lower rounds down,
    /// upper rounds up, both to a multiple of the tier's tick spacing. Invariants:
    /// <c>TickToPoolPrice(lower) &lt;= rawLowerPrice</c>, <c>TickToPoolPrice(upper) &gt;= rawUpperPrice</c>,
    /// <c>lower % spacing == 0</c>, <c>upper % spacing == 0</c>.
    /// </summary>
    public (Tick Lower, Tick Upper) ToInitializedTicks(FeeTier feeTier, TokenDecimals decimals)
    {
        // Convert both bounds to raw pool price, then sort: orientation inversion reverses ordering, so
        // this maps lower/upper explicitly rather than flipping them silently.
        var rawA = Lower.ToPoolPrice(decimals).RawToken1PerToken0;
        var rawB = Upper.ToPoolPrice(decimals).RawToken1PerToken0;
        var rawLower = Math.Min(rawA, rawB);
        var rawUpper = Math.Max(rawA, rawB);

        var spacing = feeTier.TickSpacing;

        var lowerTick = FloorToSpacing(PriceMath.PoolPriceToTick(rawLower), spacing);
        while (PriceMath.TickToPoolPrice(lowerTick) > rawLower)
        {
            lowerTick -= spacing;
        }

        var upperTick = CeilToSpacing(PriceMath.PoolPriceToTick(rawUpper), spacing);
        while (PriceMath.TickToPoolPrice(upperTick) < rawUpper)
        {
            upperTick += spacing;
        }

        return (new Tick(lowerTick), new Tick(upperTick));
    }

    // Operational tick-spacing rounding — deliberately separate from canonical price math.
    private static int FloorToSpacing(int tick, int spacing)
    {
        var remainder = tick % spacing;
        if (remainder < 0)
        {
            remainder += spacing;
        }

        return tick - remainder;
    }

    private static int CeilToSpacing(int tick, int spacing)
    {
        var floor = FloorToSpacing(tick, spacing);
        return floor == tick ? tick : floor + spacing;
    }
}
