using System.Numerics;

namespace FollowAlpha.LP.Domain.Primitives;

/// <summary>
/// Concentrated-liquidity <c>L</c>: a non-negative raw on-chain integer (<see cref="BigInteger"/>).
/// It is dimensionless in the AMM math, so it carries no decimal scaling.
///
/// <para><b>Not interchangeable with the kernel's analytics L.</b> <c>LiquidityMath</c> works in
/// analytics-grade <see cref="decimal"/>; this primitive is the raw chain value. Never compare or mix
/// the two — convert deliberately at a named boundary when raw on-chain data meets the analytics kernel.</para>
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
