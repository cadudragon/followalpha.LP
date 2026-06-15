namespace FollowAlpha.LP.Domain.Primitives;

/// <summary>
/// The decimals of a pool's two tokens, in Uniswap token order (token0, token1). Required to scale
/// between a human-readable price and the raw token1/token0 ratio that ticks encode
/// (ARCHITECTURE.md §4.1): <c>P_raw = P_human(token1/token0) · 10^(token1 − token0)</c>.
/// </summary>
public readonly record struct TokenDecimals
{
    /// <summary>Maximum supported decimals (bounded by the analytics-grade decimal scaling factor).</summary>
    public const int MaxDecimals = 28;

    /// <summary>Constructs a token-decimals pair.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Either value is outside 0..<see cref="MaxDecimals"/>.</exception>
    public TokenDecimals(int token0, int token1)
    {
        if (token0 < 0 || token0 > MaxDecimals)
        {
            throw new ArgumentOutOfRangeException(nameof(token0), token0, "Token decimals must be between 0 and 28.");
        }

        if (token1 < 0 || token1 > MaxDecimals)
        {
            throw new ArgumentOutOfRangeException(nameof(token1), token1, "Token decimals must be between 0 and 28.");
        }

        Token0 = token0;
        Token1 = token1;
    }

    /// <summary>Decimals of token0.</summary>
    public int Token0 { get; }

    /// <summary>Decimals of token1.</summary>
    public int Token1 { get; }
}
