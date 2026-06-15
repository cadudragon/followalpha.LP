namespace FollowAlpha.LP.Domain.Primitives;

/// <summary>
/// A human-readable price carrying its <see cref="PriceOrientation"/> (which token is quoted in which).
/// It cannot become a tick on its own: it must first be scaled to a raw <see cref="PoolPrice"/> with
/// the pool's <see cref="TokenDecimals"/> (ARCHITECTURE.md §4.1). This is the analytics-grade,
/// user-facing price.
/// </summary>
public readonly record struct HumanPrice
{
    /// <summary>Constructs a human price.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not strictly positive.</exception>
    public HumanPrice(decimal value, PriceOrientation orientation = PriceOrientation.Token1PerToken0)
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

    /// <summary>True when this price is in the canonical (token1-per-token0) orientation.</summary>
    public bool IsCanonical => Orientation == PriceOrientation.Token1PerToken0;

    /// <summary>The reciprocal price in the opposite orientation. (Reciprocation reverses ordering.)</summary>
    public HumanPrice Invert() => new(
        1m / Value,
        IsCanonical ? PriceOrientation.Token0PerToken1 : PriceOrientation.Token1PerToken0);

    /// <summary>This price in the canonical (token1-per-token0) orientation.</summary>
    public HumanPrice ToCanonical() => IsCanonical ? this : Invert();

    /// <summary>Scales this human price to the raw pool price using the pool's token decimals.</summary>
    public PoolPrice ToPoolPrice(TokenDecimals decimals)
    {
        var canonical = ToCanonical().Value;
        return new PoolPrice(PriceMath.CanonicalHumanToRawPrice(canonical, decimals));
    }

    /// <summary>The greatest tick whose price is ≤ this price, after decimal scaling (Uniswap floor).</summary>
    public Tick ToTick(TokenDecimals decimals) => ToPoolPrice(decimals).ToTick();
}
