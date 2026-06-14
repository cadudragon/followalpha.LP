using System.Numerics;

namespace FollowAlpha.LP.Domain.Primitives;

/// <summary>
/// The on-chain square-root price in Q64.96 fixed point: <c>sqrtPriceX96 = sqrt(price) · 2^96</c>,
/// stored raw as a <see cref="BigInteger"/>. Expresses the canonical (token1-per-token0) price.
/// Conversions delegate to <see cref="PriceMath"/>.
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

    /// <summary>The canonical price implied by this sqrt-price.</summary>
    public Price ToPrice() => new(PriceMath.SqrtPriceX96ToPrice(Value));

    /// <summary>The greatest tick whose price is ≤ the price implied by this sqrt-price.</summary>
    public Tick ToTick() => new(PriceMath.SqrtPriceX96ToTick(Value));
}
