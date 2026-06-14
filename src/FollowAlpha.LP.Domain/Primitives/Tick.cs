namespace FollowAlpha.LP.Domain.Primitives;

/// <summary>
/// A Uniswap v3 tick: an integer index into the geometric price grid <c>price = 1.0001^tick</c>.
/// Always expresses the canonical (token1-per-token0) price. Conversions delegate to
/// <see cref="PriceMath"/> so all precision policy lives in one place.
/// </summary>
public readonly record struct Tick
{
    /// <summary>Constructs a tick, validating it against the Uniswap v3 range.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Value outside <see cref="PriceMath.MinTick"/>..<see cref="PriceMath.MaxTick"/>.</exception>
    public Tick(int value)
    {
        if (value < PriceMath.MinTick || value > PriceMath.MaxTick)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Tick is outside the Uniswap v3 range.");
        }

        Value = value;
    }

    /// <summary>The tick index.</summary>
    public int Value { get; }

    /// <summary>The lowest valid tick.</summary>
    public static Tick Min => new(PriceMath.MinTick);

    /// <summary>The highest valid tick.</summary>
    public static Tick Max => new(PriceMath.MaxTick);

    /// <summary>The canonical price at this tick (<c>1.0001^tick</c>).</summary>
    public Price ToPrice() => new(PriceMath.TickToPrice(Value));

    /// <summary>The <c>sqrtPriceX96</c> at this tick.</summary>
    public SqrtPriceX96 ToSqrtPriceX96() => new(PriceMath.TickToSqrtPriceX96(Value));
}
