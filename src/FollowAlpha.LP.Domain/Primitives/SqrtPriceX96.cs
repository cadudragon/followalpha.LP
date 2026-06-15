using System.Numerics;

namespace FollowAlpha.LP.Domain.Primitives;

/// <summary>
/// The on-chain square-root price in Q64.96 fixed point: <c>sqrt(token1/token0) · 2^96</c>, stored raw
/// as a <see cref="BigInteger"/>. <see cref="ToTick"/> is exact (<see cref="TickMath"/>);
/// <see cref="ToPoolPrice"/> is the analytics-grade decimal view.
/// </summary>
public readonly record struct SqrtPriceX96
{
    /// <summary>Constructs a sqrt-price from its raw Q64.96 integer.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not strictly positive.</exception>
    public SqrtPriceX96(BigInteger value)
    {
        if (value <= BigInteger.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "sqrtPriceX96 must be strictly positive.");
        }

        Value = value;
    }

    /// <summary>The raw Q64.96 integer.</summary>
    public BigInteger Value { get; }

    /// <summary>The exact tick for this sqrt-price (greatest tick whose ratio ≤ this value).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Value outside <c>[MinSqrtRatio, MaxSqrtRatio)</c>.</exception>
    public Tick ToTick() => new(TickMath.GetTickAtSqrtRatio(Value));

    /// <summary>The analytics-grade decimal pool price implied by this sqrt-price (<c>(s/2^96)^2</c>).</summary>
    /// <exception cref="PriceOutsideDecimalRangeException">The price falls outside the decimal window.</exception>
    public PoolPrice ToPoolPrice() => new(PriceMath.SqrtPriceX96ToPoolPrice(Value));
}
