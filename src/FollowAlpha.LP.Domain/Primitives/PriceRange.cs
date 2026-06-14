namespace FollowAlpha.LP.Domain.Primitives;

/// <summary>
/// A requested price band [<see cref="Lower"/>, <see cref="Upper"/>], both bounds in the same
/// orientation. <see cref="ToInitializedTicks"/> maps it to initialized ticks for a fee tier — a
/// distinct concern from the canonical math: it rounds <i>outward</i> (lower down, upper up) so the
/// returned tick band always <b>contains</b> the request and never silently narrows it
/// (ARCHITECTURE.md §4.1, "Range-boundary conversion").
/// </summary>
public readonly record struct PriceRange
{
    /// <summary>Constructs a range; bounds must share an orientation and be strictly ordered.</summary>
    /// <exception cref="ArgumentException">Bounds have different orientations, or <paramref name="lower"/> is not below <paramref name="upper"/>.</exception>
    public PriceRange(Price lower, Price upper)
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

    /// <summary>The lower price bound (in <see cref="Price.Orientation"/>).</summary>
    public Price Lower { get; }

    /// <summary>The upper price bound (in <see cref="Price.Orientation"/>).</summary>
    public Price Upper { get; }

    /// <summary>
    /// The initialized tick band, in canonical orientation, that contains this range for
    /// <paramref name="feeTier"/>. The lower tick rounds down and the upper tick rounds up, both to a
    /// multiple of the tier's tick spacing. Invariants:
    /// <c>TickToPrice(lower) &lt;= canonicalLowerPrice</c>, <c>TickToPrice(upper) &gt;= canonicalUpperPrice</c>,
    /// <c>lower % spacing == 0</c>, <c>upper % spacing == 0</c>.
    /// </summary>
    public (Tick Lower, Tick Upper) ToInitializedTicks(FeeTier feeTier)
    {
        // Map to canonical orientation explicitly. Reciprocation reverses order, so when the bounds are
        // inverted the requested lower/upper swap roles — handled here, never silently.
        var canonicalLower = Lower.IsCanonical ? Lower.Value : Upper.ToCanonical().Value;
        var canonicalUpper = Lower.IsCanonical ? Upper.Value : Lower.ToCanonical().Value;

        var spacing = feeTier.TickSpacing;

        // Lower: floor to spacing, then step down until the tick's price is at or below the request.
        var lowerTick = FloorToSpacing(PriceMath.PriceToTick(canonicalLower), spacing);
        while (PriceMath.TickToPrice(lowerTick) > canonicalLower)
        {
            lowerTick -= spacing;
        }

        // Upper: ceil to spacing, then step up until the tick's price is at or above the request.
        var upperTick = CeilToSpacing(PriceMath.PriceToTick(canonicalUpper), spacing);
        while (PriceMath.TickToPrice(upperTick) < canonicalUpper)
        {
            upperTick += spacing;
        }

        return (new Tick(lowerTick), new Tick(upperTick));
    }

    // Operational tick-spacing rounding — deliberately separate from canonical price math (PriceMath).
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
