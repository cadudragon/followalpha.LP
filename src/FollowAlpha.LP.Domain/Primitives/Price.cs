namespace FollowAlpha.LP.Domain.Primitives;

/// <summary>
/// A strictly-positive price carrying its <see cref="PriceOrientation"/>. Conversions to tick /
/// sqrt-price are defined in the canonical (token1-per-token0) orientation; a non-canonical price is
/// mapped via <see cref="ToCanonical"/> before conversion, so orientation is honored explicitly and
/// never flipped by accident (ARCHITECTURE.md §4.1).
/// </summary>
public readonly record struct Price
{
    /// <summary>Constructs a price.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not strictly positive.</exception>
    public Price(decimal value, PriceOrientation orientation = PriceOrientation.Token1PerToken0)
    {
        if (value <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Price must be strictly positive.");
        }

        Value = value;
        Orientation = orientation;
    }

    /// <summary>The numeric price in its <see cref="Orientation"/>.</summary>
    public decimal Value { get; }

    /// <summary>The ratio this price expresses.</summary>
    public PriceOrientation Orientation { get; }

    /// <summary>True when this price is already in the canonical (token1-per-token0) orientation.</summary>
    public bool IsCanonical => Orientation == PriceOrientation.Token1PerToken0;

    /// <summary>
    /// The reciprocal price in the opposite orientation. Reciprocation is monotonically decreasing, so
    /// callers that order prices (e.g. range bounds) must account for the swap — see
    /// <see cref="PriceRange.ToInitializedTicks"/>.
    /// </summary>
    public Price Invert() => new(
        1m / Value,
        IsCanonical ? PriceOrientation.Token0PerToken1 : PriceOrientation.Token1PerToken0);

    /// <summary>This price expressed in the canonical (token1-per-token0) orientation.</summary>
    public Price ToCanonical() => IsCanonical ? this : Invert();

    /// <summary>The greatest tick whose price is ≤ this price (Uniswap floor semantics).</summary>
    public Tick ToTick() => new(PriceMath.PriceToTick(ToCanonical().Value));

    /// <summary>The <c>sqrtPriceX96</c> for this price.</summary>
    public SqrtPriceX96 ToSqrtPriceX96() => new(PriceMath.PriceToSqrtPriceX96(ToCanonical().Value));
}
