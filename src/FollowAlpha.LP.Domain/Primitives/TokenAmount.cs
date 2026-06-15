using System.Numerics;

namespace FollowAlpha.LP.Domain.Primitives;

/// <summary>
/// A non-negative token amount holding both the raw on-chain integer (base units,
/// <see cref="BigInteger"/>) and the token's decimals. The raw ↔ human-scale <see cref="decimal"/>
/// conversion lives here. Construction from a human value is always explicit about rounding — there is
/// no silent default — because on-chain base units must not be invented (ARCHITECTURE.md §4.1).
/// Signed variation is a separate concept (a future <c>TokenDelta</c>), not this type.
/// </summary>
public readonly record struct TokenAmount
{
    /// <summary>Maximum supported token decimals (bounded by the decimal range).</summary>
    public const int MaxDecimals = 28;

    /// <summary>Constructs a token amount from raw base units.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="raw"/> is negative, or <paramref name="decimals"/> is outside 0..<see cref="MaxDecimals"/>.</exception>
    public TokenAmount(BigInteger raw, int decimals)
    {
        if (raw < BigInteger.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(raw), "Token amount cannot be negative.");
        }

        GuardDecimals(decimals);

        Raw = raw;
        Decimals = decimals;
    }

    /// <summary>The raw on-chain amount in base units (non-negative).</summary>
    public BigInteger Raw { get; }

    /// <summary>The token's decimals.</summary>
    public int Decimals { get; }

    /// <summary>The human-scale amount (<c>Raw / 10^Decimals</c>).</summary>
    /// <exception cref="OverflowException">The integer part falls outside the decimal range.</exception>
    public decimal ToDecimal()
    {
        var divisor = BigInteger.Pow(10, Decimals);
        var integer = BigInteger.DivRem(Raw, divisor, out var remainder);
        return (decimal)integer + (decimal)remainder / (decimal)divisor;
    }

    /// <summary>
    /// Builds a token amount from a human value that is <b>exactly</b> representable in base units.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="human"/> is negative, or <paramref name="decimals"/> is out of range.</exception>
    /// <exception cref="ArgumentException"><paramref name="human"/> has finer precision than <paramref name="decimals"/> base units.</exception>
    public static TokenAmount FromDecimalExact(decimal human, int decimals)
    {
        var scaled = ScaleHuman(human, decimals);
        if (scaled != decimal.Truncate(scaled))
        {
            throw new ArgumentException(
                "Human value is not exactly representable at the given decimals.", nameof(human));
        }

        return new TokenAmount((BigInteger)scaled, decimals);
    }

    /// <summary>Builds a token amount, truncating any sub-unit remainder toward zero (floor for non-negative input).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="human"/> is negative, or <paramref name="decimals"/> is out of range.</exception>
    public static TokenAmount FromDecimalFloor(decimal human, int decimals)
    {
        var scaled = ScaleHuman(human, decimals);
        return new TokenAmount((BigInteger)decimal.Truncate(scaled), decimals);
    }

    /// <summary>Builds a token amount, rounding any sub-unit remainder with the explicitly chosen mode.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="human"/> is negative, or <paramref name="decimals"/> is out of range.</exception>
    public static TokenAmount FromDecimalRounded(decimal human, int decimals, MidpointRounding mode)
    {
        var scaled = ScaleHuman(human, decimals);
        return new TokenAmount((BigInteger)Math.Round(scaled, 0, mode), decimals);
    }

    private static decimal ScaleHuman(decimal human, int decimals)
    {
        if (human < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(human), human, "Token amount cannot be negative.");
        }

        GuardDecimals(decimals);
        return human * Pow10Decimal(decimals);
    }

    private static void GuardDecimals(int decimals)
    {
        if (decimals < 0 || decimals > MaxDecimals)
        {
            throw new ArgumentOutOfRangeException(nameof(decimals), decimals, "Decimals must be between 0 and 28.");
        }
    }

    private static decimal Pow10Decimal(int n)
    {
        var result = 1m;
        for (var i = 0; i < n; i++)
        {
            result *= 10m;
        }

        return result;
    }
}
