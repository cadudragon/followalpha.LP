using FollowAlpha.LP.Domain.Primitives;

namespace FollowAlpha.LP.Domain.Kernel;

/// <summary>
/// The concentrated-liquidity math kernel — a faithful C# port of Atis Elsts' reference
/// (<c>tools/oracle/reference/uniswap-v3-liquidity-math.py</c>, from the note "Liquidity Math in
/// Uniswap v3"). It is the analytics core for Modules 0/2/3: liquidity from amounts+range, amounts
/// from liquidity+price, range bounds from amounts, and inventory deltas as price moves.
///
/// <para><b>Numeric policy.</b> Analytics-grade <see cref="decimal"/> (not <c>double</c> like the
/// reference): deterministic across platforms (NFR D1). All inputs are <b>square-root prices</b>
/// (<c>sqrt(price)</c>) and human-scale amounts/liquidity, matching the reference's variables
/// (<c>sp = sqrt(P)</c>, <c>sa = sqrt(a)</c>, <c>sb = sqrt(b)</c>). The C# kernel converges to the
/// Python oracle within the tolerance documented in the golden fixtures — never the reverse
/// (AGENTS.md hard rule 3). Use <see cref="PriceMath.Sqrt"/> to obtain a sqrt price from a price.</para>
///
/// <para>Unused parameters from the reference signatures (e.g. <c>sb</c> in <c>calculate_a1</c>) are
/// dropped; the arithmetic is identical.</para>
/// </summary>
public static class LiquidityMath
{
    /// <summary>Liquidity from amount of token0 over a fully-above range: <c>x·sa·sb/(sb−sa)</c>.</summary>
    public static decimal GetLiquidity0(decimal x, decimal sqrtLower, decimal sqrtUpper) =>
        x * sqrtLower * sqrtUpper / (sqrtUpper - sqrtLower);

    /// <summary>Liquidity from amount of token1 over a fully-below range: <c>y/(sb−sa)</c>.</summary>
    public static decimal GetLiquidity1(decimal y, decimal sqrtLower, decimal sqrtUpper) =>
        y / (sqrtUpper - sqrtLower);

    /// <summary>
    /// Liquidity for a position holding <paramref name="x"/> token0 and <paramref name="y"/> token1 at
    /// the current sqrt price, for the range [<paramref name="sqrtLower"/>, <paramref name="sqrtUpper"/>].
    /// Below the range it is token0-only; in range it is the binding minimum of the two sides; above it
    /// is token1-only.
    /// </summary>
    public static decimal GetLiquidity(decimal x, decimal y, decimal sqrtPrice, decimal sqrtLower, decimal sqrtUpper)
    {
        if (sqrtPrice <= sqrtLower)
        {
            return GetLiquidity0(x, sqrtLower, sqrtUpper);
        }

        if (sqrtPrice < sqrtUpper)
        {
            var liquidity0 = GetLiquidity0(x, sqrtPrice, sqrtUpper);
            var liquidity1 = GetLiquidity1(y, sqrtLower, sqrtPrice);
            return Math.Min(liquidity0, liquidity1);
        }

        return GetLiquidity1(y, sqrtLower, sqrtUpper);
    }

    /// <summary>Amount of token0 for liquidity at a price (price clamped into the range): <c>L·(sb−sp)/(sp·sb)</c>.</summary>
    public static decimal CalculateX(decimal liquidity, decimal sqrtPrice, decimal sqrtLower, decimal sqrtUpper)
    {
        var sp = Clamp(sqrtPrice, sqrtLower, sqrtUpper);
        return liquidity * (sqrtUpper - sp) / (sp * sqrtUpper);
    }

    /// <summary>Amount of token1 for liquidity at a price (price clamped into the range): <c>L·(sp−sa)</c>.</summary>
    public static decimal CalculateY(decimal liquidity, decimal sqrtPrice, decimal sqrtLower, decimal sqrtUpper)
    {
        var sp = Clamp(sqrtPrice, sqrtLower, sqrtUpper);
        return liquidity * (sp - sqrtLower);
    }

    /// <summary>Lower range bound (price a) from liquidity and amounts: <c>(sp − y/L)^2</c>.</summary>
    public static decimal CalculateA1(decimal liquidity, decimal sqrtPrice, decimal y)
    {
        var sqrtLower = sqrtPrice - y / liquidity;
        return sqrtLower * sqrtLower;
    }

    /// <summary>Lower range bound (price a) from amounts and the upper bound, without liquidity.</summary>
    public static decimal CalculateA2(decimal sqrtPrice, decimal sqrtUpper, decimal x, decimal y)
    {
        var sqrtLower = y / (sqrtUpper * x) + sqrtPrice - y / (sqrtPrice * x);
        return sqrtLower * sqrtLower;
    }

    /// <summary>Upper range bound (price b) from liquidity and amounts: <c>((L·sp)/(L − sp·x))^2</c>.</summary>
    public static decimal CalculateB1(decimal liquidity, decimal sqrtPrice, decimal x)
    {
        var sqrtUpper = liquidity * sqrtPrice / (liquidity - sqrtPrice * x);
        return sqrtUpper * sqrtUpper;
    }

    /// <summary>Upper range bound (price b) from amounts and the lower bound, without liquidity.</summary>
    public static decimal CalculateB2(decimal sqrtPrice, decimal sqrtLower, decimal x, decimal y)
    {
        var p = sqrtPrice * sqrtPrice;
        var sqrtUpper = sqrtPrice * y / ((sqrtLower * sqrtPrice - p) * x + y);
        return sqrtUpper * sqrtUpper;
    }

    /// <summary>Ratio <c>c = (b/P)</c> reconstructed from <paramref name="d"/> and the amounts (whitepaper relation).</summary>
    public static decimal CalculateC(decimal price, decimal d, decimal x, decimal y) =>
        y / ((d - 1m) * price * x + y);

    /// <summary>Ratio <c>d = (a/P)</c> reconstructed from <paramref name="c"/> and the amounts (whitepaper relation).</summary>
    public static decimal CalculateD(decimal price, decimal c, decimal x, decimal y) =>
        1m + y * (1m - c) / (c * price * x);

    /// <summary>
    /// Inventory change as the price moves from <paramref name="sqrtPrice"/> to
    /// <paramref name="sqrtPriceNext"/> (whitepaper delta form), both clamped into the range:
    /// <c>Δx = (1/sp1 − 1/sp)·L</c>, <c>Δy = (sp1 − sp)·L</c>.
    /// </summary>
    public static (decimal DeltaX, decimal DeltaY) AmountDeltas(
        decimal liquidity,
        decimal sqrtPrice,
        decimal sqrtPriceNext,
        decimal sqrtLower,
        decimal sqrtUpper)
    {
        var sp = Clamp(sqrtPrice, sqrtLower, sqrtUpper);
        var sp1 = Clamp(sqrtPriceNext, sqrtLower, sqrtUpper);
        var deltaY = (sp1 - sp) * liquidity;
        var deltaX = (1m / sp1 - 1m / sp) * liquidity;
        return (deltaX, deltaY);
    }

    private static decimal Clamp(decimal sqrtPrice, decimal sqrtLower, decimal sqrtUpper) =>
        Math.Max(Math.Min(sqrtPrice, sqrtUpper), sqrtLower);
}
