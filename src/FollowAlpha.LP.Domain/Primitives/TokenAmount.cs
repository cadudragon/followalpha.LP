using System.Numerics;

namespace FollowAlpha.LP.Domain.Primitives;

/// <summary>
/// A token amount holding both the raw on-chain integer (base units, <see cref="BigInteger"/>) and the
/// token's decimals, with the raw ↔ human-scale <see cref="decimal"/> conversion living here as part of
/// the single precision policy (AGENTS.md "Money/price math"; ARCHITECTURE.md §4.1).
/// </summary>
public readonly record struct TokenAmount
{
    /// <summary>Maximum supported token decimals (covers ERC-20 in scope; bounded by decimal range).</summary>
    public const int MaxDecimals = 28;

    /// <summary>Constructs a token amount from raw base units.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="decimals"/> is outside 0..<see cref="MaxDecimals"/>.</exception>
    public TokenAmount(BigInteger raw, int decimals)
    {
        if (decimals < 0 || decimals > MaxDecimals)
        {
            throw new ArgumentOutOfRangeException(nameof(decimals), decimals, "Decimals must be between 0 and 28.");
        }

        Raw = raw;
        Decimals = decimals;
    }

    /// <summary>The raw on-chain amount in base units.</summary>
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

    /// <summary>Builds a token amount from a human-scale value, rounding to the nearest base unit (banker's rounding).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="decimals"/> is outside 0..<see cref="MaxDecimals"/>.</exception>
    /// <exception cref="OverflowException">The scaled value falls outside the decimal range.</exception>
    public static TokenAmount FromDecimal(decimal human, int decimals)
    {
        if (decimals < 0 || decimals > MaxDecimals)
        {
            throw new ArgumentOutOfRangeException(nameof(decimals), decimals, "Decimals must be between 0 and 28.");
        }

        var scaled = human * Pow10Decimal(decimals);
        var rounded = Math.Round(scaled, 0, MidpointRounding.ToEven);
        return new TokenAmount((BigInteger)rounded, decimals);
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
