namespace FollowAlpha.LP.Domain.Primitives;

/// <summary>
/// A Uniswap v3 tick: an integer index into the geometric price grid <c>price = 1.0001^tick</c>.
/// Models the raw on-chain tick over the full Uniswap range. <see cref="ToSqrtPriceX96"/> is exact
/// (<see cref="TickMath"/>); <see cref="ToPoolPrice"/> is the analytics-grade decimal view.
/// </summary>
public readonly record struct Tick
{
    /// <summary>Constructs a tick, validating it against the Uniswap v3 range.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Value outside <see cref="TickMath.MinTick"/>..<see cref="TickMath.MaxTick"/>.</exception>
    public Tick(int value)
    {
        if (value < TickMath.MinTick || value > TickMath.MaxTick)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Tick is outside the Uniswap v3 range.");
        }

        Value = value;
    }

    /// <summary>The tick index.</summary>
    public int Value { get; }

    /// <summary>The lowest valid tick.</summary>
    public static Tick Min => new(TickMath.MinTick);

    /// <summary>The highest valid tick.</summary>
    public static Tick Max => new(TickMath.MaxTick);

    /// <summary>The exact raw <c>sqrtPriceX96</c> at this tick (on-chain-faithful, full range).</summary>
    public SqrtPriceX96 ToSqrtPriceX96() => new(TickMath.GetSqrtRatioAtTick(Value));

    /// <summary>The analytics-grade decimal pool price (<c>1.0001^tick</c>, token1/token0).</summary>
    /// <exception cref="PriceOutsideDecimalRangeException">The price falls outside the decimal window.</exception>
    public PoolPrice ToPoolPrice() => new(PriceMath.TickToPoolPrice(Value));
}
