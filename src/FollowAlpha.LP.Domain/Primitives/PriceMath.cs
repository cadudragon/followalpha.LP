using System.Numerics;

namespace FollowAlpha.LP.Domain.Primitives;

/// <summary>
/// The single home for the <b>analytics decimal view</b> and the precision policy around it. The exact
/// raw integer engine for <c>Tick ↔ SqrtPriceX96</c> lives in <see cref="TickMath"/>; this class covers
/// the decimal side that downstream analytics use, and the human↔raw decimal scaling (ARCHITECTURE.md §4.1).
///
/// <para><b>Precision policy.</b></para>
/// <list type="bullet">
///   <item><b>Separation.</b> Raw/on-chain/canonical values are integers (<see cref="Tick"/>,
///   <see cref="SqrtPriceX96"/>) and exact (<see cref="TickMath"/>). The decimal pool price
///   (<c>1.0001^tick</c>) and human price are <b>analytics-grade</b> views — never used where the raw
///   on-chain integer is required.</item>
///   <item><b>tick → decimal pool price.</b> <c>1.0001^tick</c> via integer exponentiation-by-squaring
///   in <see cref="decimal"/> (deterministic; no <see cref="Math.Pow(double,double)"/>). Outside the
///   representable decimal window it throws <see cref="PriceOutsideDecimalRangeException"/>.</item>
///   <item><b>decimal pool price → tick.</b> Uniswap floor semantics with a verified ±1 guard against
///   the exact decimal invariant <c>TickToPoolPrice(t) &lt;= price &lt; TickToPoolPrice(t+1)</c>. A
///   double log only seeds the candidate; the returned integer never depends on its rounding.</item>
///   <item><b>decimal scaling.</b> <c>P_raw = P_human(token1/token0) · 10^(dec1 − dec0)</c>, all in this
///   one location.</item>
/// </list>
/// </summary>
public static class PriceMath
{
    /// <summary>The tick base: <c>price = TickBase^tick</c>.</summary>
    public const decimal TickBase = 1.0001m;

    /// <summary>Minimum tick (mirrors <see cref="TickMath.MinTick"/>).</summary>
    public const int MinTick = TickMath.MinTick;

    /// <summary>Maximum tick (mirrors <see cref="TickMath.MaxTick"/>).</summary>
    public const int MaxTick = TickMath.MaxTick;

    /// <summary>Q96 = 2^96, the fixed-point scale of <c>sqrtPriceX96</c>.</summary>
    public static readonly BigInteger Q96 = BigInteger.One << 96;

    /// <summary>Fractional decimal digits carried through the BigInteger step in sqrt-price → decimal.</summary>
    public const int SqrtPriceScaleDigits = 18;

    /// <summary>
    /// Relative tolerance for analytics round-trips that pass through <c>sqrtPriceX96</c> as a decimal
    /// (e.g. <c>Tick → SqrtPriceX96 → decimal pool price</c> vs <c>1.0001^tick</c>). Generous relative
    /// to the ~1e-18 fixed-point resolution.
    /// </summary>
    public const decimal SqrtRoundTripRelativeTolerance = 1e-12m;

    /// <summary>The analytics decimal pool price at <paramref name="tick"/> (<c>1.0001^tick</c>, token1/token0).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Tick outside <see cref="MinTick"/>..<see cref="MaxTick"/>.</exception>
    /// <exception cref="PriceOutsideDecimalRangeException">The price falls outside the decimal window.</exception>
    public static decimal TickToPoolPrice(int tick)
    {
        if (tick < MinTick || tick > MaxTick)
        {
            throw new ArgumentOutOfRangeException(nameof(tick), tick, "Tick is outside the Uniswap v3 range.");
        }

        try
        {
            var price = tick >= 0 ? PowInt(TickBase, tick) : 1m / PowInt(TickBase, -tick);
            if (price <= 0m)
            {
                throw new PriceOutsideDecimalRangeException(
                    "Tick price underflows the analytics-grade decimal window.");
            }

            return price;
        }
        catch (OverflowException ex)
        {
            throw new PriceOutsideDecimalRangeException(
                "Tick price overflows the analytics-grade decimal window.", ex);
        }
    }

    /// <summary>
    /// The greatest tick whose decimal pool price is ≤ <paramref name="poolPrice"/> (Uniswap floor),
    /// guaranteeing <c>TickToPoolPrice(result) &lt;= poolPrice &lt; TickToPoolPrice(result+1)</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="poolPrice"/> is not strictly positive.</exception>
    public static int PoolPriceToTick(decimal poolPrice)
    {
        if (poolPrice <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(poolPrice), poolPrice, "Pool price must be strictly positive.");
        }

        var seed = Math.Log((double)poolPrice) / Math.Log((double)TickBase);
        var candidate = Math.Clamp((int)Math.Floor(seed), MinTick, MaxTick);

        while (candidate > MinTick && TickToPoolPrice(candidate) > poolPrice)
        {
            candidate--;
        }

        while (candidate < MaxTick && TickToPoolPrice(candidate + 1) <= poolPrice)
        {
            candidate++;
        }

        return candidate;
    }

    /// <summary>The analytics decimal pool price implied by a raw <c>sqrtPriceX96</c> (<c>(s/2^96)^2</c>).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sqrtPriceX96"/> is not strictly positive.</exception>
    /// <exception cref="PriceOutsideDecimalRangeException">The price falls outside the decimal window.</exception>
    public static decimal SqrtPriceX96ToPoolPrice(BigInteger sqrtPriceX96)
    {
        if (sqrtPriceX96 <= BigInteger.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(sqrtPriceX96), "sqrtPriceX96 must be strictly positive.");
        }

        try
        {
            var scaled = sqrtPriceX96 * Pow10BigInteger(SqrtPriceScaleDigits) / Q96;
            var sqrtPrice = (decimal)scaled / Pow10Decimal(SqrtPriceScaleDigits);
            return sqrtPrice * sqrtPrice;
        }
        catch (OverflowException ex)
        {
            throw new PriceOutsideDecimalRangeException(
                "sqrtPriceX96 price overflows the analytics-grade decimal window.", ex);
        }
    }

    /// <summary>Scales a canonical (token1/token0) human price to the raw pool price: <c>· 10^(dec1 − dec0)</c>.</summary>
    public static decimal CanonicalHumanToRawPrice(decimal canonicalHumanPrice, TokenDecimals decimals)
    {
        var diff = decimals.Token1 - decimals.Token0;
        return diff >= 0
            ? canonicalHumanPrice * Pow10Decimal(diff)
            : canonicalHumanPrice / Pow10Decimal(-diff);
    }

    /// <summary>Scales a raw pool price to the canonical (token1/token0) human price: <c>· 10^(dec0 − dec1)</c>.</summary>
    public static decimal RawPriceToCanonicalHuman(decimal rawPoolPrice, TokenDecimals decimals)
    {
        var diff = decimals.Token1 - decimals.Token0;
        return diff >= 0
            ? rawPoolPrice / Pow10Decimal(diff)
            : rawPoolPrice * Pow10Decimal(-diff);
    }

    /// <summary>Integer exponentiation by squaring in decimal. <paramref name="exponent"/> must be ≥ 0.</summary>
    private static decimal PowInt(decimal value, int exponent)
    {
        var result = 1m;
        var factor = value;
        var n = exponent;
        while (n > 0)
        {
            if ((n & 1) == 1)
            {
                result *= factor;
            }

            n >>= 1;
            if (n > 0)
            {
                factor *= factor;
            }
        }

        return result;
    }

    private static decimal Pow10Decimal(int n)
    {
        if (n < 0 || n > 28)
        {
            throw new ArgumentOutOfRangeException(nameof(n), n, "10^n is outside the decimal range.");
        }

        var result = 1m;
        for (var i = 0; i < n; i++)
        {
            result *= 10m;
        }

        return result;
    }

    private static BigInteger Pow10BigInteger(int n) => BigInteger.Pow(10, n);
}
