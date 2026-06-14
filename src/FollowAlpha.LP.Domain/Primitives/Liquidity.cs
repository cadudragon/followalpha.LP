using System.Numerics;

namespace FollowAlpha.LP.Domain.Primitives;

/// <summary>
/// Concentrated-liquidity <c>L</c>: a non-negative raw on-chain integer (<see cref="BigInteger"/>).
/// It is dimensionless in the AMM math, so it carries no decimal scaling — only the liquidity-math
/// kernel (item 1.2) combines it with prices.
/// </summary>
public readonly record struct Liquidity
{
    /// <summary>Constructs a liquidity value.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    public Liquidity(BigInteger value)
    {
        if (value < BigInteger.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Liquidity cannot be negative.");
        }

        Value = value;
    }

    /// <summary>The raw liquidity <c>L</c>.</summary>
    public BigInteger Value { get; }

    /// <summary>Zero liquidity.</summary>
    public static Liquidity Zero => new(BigInteger.Zero);
}
