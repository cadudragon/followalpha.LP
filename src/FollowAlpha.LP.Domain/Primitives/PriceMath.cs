using System.Numerics;

namespace FollowAlpha.LP.Domain.Primitives;

/// <summary>
/// The single home for every raw-integer ↔ decimal conversion in the Domain and for the
/// precision policy that governs them (ARCHITECTURE.md §4.1). Nothing else in the kernel may
/// re-implement tick/price/sqrt-price math: it all routes through here so precision lives in one place.
///
/// <para><b>Precision policy.</b></para>
/// <list type="bullet">
///   <item><b>Types.</b> Human-scale prices are <see cref="decimal"/> (analytics-grade, ARCHITECTURE §4.1).
///   Raw on-chain integers — <c>sqrtPriceX96</c> (Q64.96), liquidity, token base units — are
///   <see cref="BigInteger"/>. Ticks are <see cref="int"/>.</item>
///   <item><b>Determinism (NFR D1).</b> Final values are produced by <see cref="decimal"/> and
///   <see cref="BigInteger"/> arithmetic only — both bit-for-bit reproducible across platforms.
///   <see cref="Math"/> (double) is used <i>only</i> to seed an estimate that is then corrected by an
///   exact integer/decimal invariant, so the returned value never depends on double rounding.</item>
///   <item><b>tick → price.</b> <c>price = 1.0001^tick</c> computed by integer exponentiation-by-squaring
///   in <see cref="decimal"/> (no <see cref="Math.Pow(double,double)"/>): deterministic and exact to
///   decimal precision.</item>
///   <item><b>price → tick.</b> Uniswap v3 <c>TickMath.getTickAtSqrtRatio</c> semantics: the greatest
///   tick whose price ≤ the given price (floor in tick space), with the verified invariant
///   <c>TickToPrice(tick) &lt;= price &lt; TickToPrice(tick+1)</c>. A double log seeds the candidate;
///   the candidate is then corrected ±1 against the exact decimal invariant (the Uniswap guard step) —
///   never a raw <c>floor(log/log)</c>.</item>
///   <item><b>sqrt-price.</b> Square root via deterministic decimal Newton iteration; the
///   multiply/divide by 2^96 is done in <see cref="BigInteger"/> so it never overflows
///   <see cref="decimal"/>. A fixed-point scale of 10^<see cref="SqrtPriceScaleDigits"/> carries the
///   fractional sqrt-price through the integer step.</item>
///   <item><b>Range window.</b> "Analytics-grade decimal" means the representable magnitude window of
///   <see cref="decimal"/> (~1e-28 … 7.9e28). Prices outside it — i.e. ticks past roughly ±6.6e5, which
///   no real asset reaches — overflow by design and throw <see cref="OverflowException"/>. The Uniswap
///   tick range <see cref="MinTick"/>..<see cref="MaxTick"/> is the validation range for the
///   <see cref="Tick"/> type; the extreme tail is intentionally out of the supported price window.</item>
/// </list>
/// Tolerances for the kernel's golden tests are defined with those tests (item 1.2). The constants here
/// (<see cref="SqrtRoundTripRelativeTolerance"/>) document the tolerance used by the primitives' own
/// round-trip unit tests, which is the only place tolerance is needed at this layer.
/// </summary>
public static class PriceMath
{
    /// <summary>The tick base: <c>price = TickBase^tick</c>.</summary>
    public const decimal TickBase = 1.0001m;

    /// <summary>Minimum tick (Uniswap v3 <c>TickMath.MIN_TICK</c>).</summary>
    public const int MinTick = -887272;

    /// <summary>Maximum tick (Uniswap v3 <c>TickMath.MAX_TICK</c>).</summary>
    public const int MaxTick = 887272;

    /// <summary>Q96 = 2^96, the fixed-point scale of <c>sqrtPriceX96</c> (Q64.96).</summary>
    public static readonly BigInteger Q96 = BigInteger.One << 96;

    /// <summary>
    /// Fractional decimal digits carried through the BigInteger fixed-point step when converting
    /// sqrt-price to/from <c>sqrtPriceX96</c>. 18 digits is far beyond any verdict-relevant resolution.
    /// </summary>
    public const int SqrtPriceScaleDigits = 18;

    /// <summary>
    /// Relative tolerance for price round-trips that pass through the sqrt / Q96 fixed-point path
    /// (<c>price → sqrtPriceX96 → price</c>). Documented here so the primitives' round-trip tests share
    /// one number. Generous relative to the ~1e-18 fixed-point resolution.
    /// </summary>
    public const decimal SqrtRoundTripRelativeTolerance = 1e-12m;

    /// <summary><c>price = 1.0001^tick</c> (token1-per-token0, the canonical orientation).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Tick outside <see cref="MinTick"/>..<see cref="MaxTick"/>.</exception>
    /// <exception cref="OverflowException">The price falls outside the analytics-grade decimal window.</exception>
    public static decimal TickToPrice(int tick)
    {
        GuardTickRange(tick);
        return tick >= 0
            ? PowInt(TickBase, tick)
            : 1m / PowInt(TickBase, -tick);
    }

    /// <summary>
    /// The greatest tick whose price is ≤ <paramref name="price"/> (Uniswap floor semantics).
    /// Guarantees <c>TickToPrice(result) &lt;= price &lt; TickToPrice(result+1)</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Price is not strictly positive.</exception>
    public static int PriceToTick(decimal price)
    {
        if (price <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(price), price, "Price must be strictly positive.");
        }

        // Seed with a double estimate; the result does not depend on its rounding — the loops below
        // correct it against the exact decimal invariant, so the integer returned is deterministic.
        var seed = Math.Log((double)price) / Math.Log((double)TickBase);
        var candidate = Math.Clamp((int)Math.Floor(seed), MinTick, MaxTick);

        // Pull down while the candidate's price exceeds the target.
        while (candidate > MinTick && TickToPrice(candidate) > price)
        {
            candidate--;
        }

        // Push up while the next tick still fits at or below the target.
        while (candidate < MaxTick && TickToPrice(candidate + 1) <= price)
        {
            candidate++;
        }

        return candidate;
    }

    /// <summary><c>sqrtPriceX96 = floor(sqrt(price) · 2^96)</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Price is not strictly positive.</exception>
    public static BigInteger PriceToSqrtPriceX96(decimal price)
    {
        if (price <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(price), price, "Price must be strictly positive.");
        }

        var sqrtPrice = Sqrt(price);
        // floor(sqrtPrice · 2^96), done in BigInteger so the 2^96 factor never overflows decimal.
        var scaled = (BigInteger)(sqrtPrice * Pow10Decimal(SqrtPriceScaleDigits));
        return scaled * Q96 / Pow10BigInteger(SqrtPriceScaleDigits);
    }

    /// <summary><c>price = (sqrtPriceX96 / 2^96)^2</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sqrtPriceX96"/> is not strictly positive.</exception>
    /// <exception cref="OverflowException">The price falls outside the analytics-grade decimal window.</exception>
    public static decimal SqrtPriceX96ToPrice(BigInteger sqrtPriceX96)
    {
        if (sqrtPriceX96 <= BigInteger.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(sqrtPriceX96), "sqrtPriceX96 must be strictly positive.");
        }

        // sqrtPrice = sqrtPriceX96 / 2^96, taken to fixed-point in BigInteger to dodge decimal overflow.
        var scaled = sqrtPriceX96 * Pow10BigInteger(SqrtPriceScaleDigits) / Q96;
        var sqrtPrice = (decimal)scaled / Pow10Decimal(SqrtPriceScaleDigits);
        return sqrtPrice * sqrtPrice;
    }

    /// <summary><c>sqrtPriceX96</c> at <paramref name="tick"/>.</summary>
    public static BigInteger TickToSqrtPriceX96(int tick) => PriceToSqrtPriceX96(TickToPrice(tick));

    /// <summary>The greatest tick whose price is ≤ the price implied by <paramref name="sqrtPriceX96"/>.</summary>
    public static int SqrtPriceX96ToTick(BigInteger sqrtPriceX96) => PriceToTick(SqrtPriceX96ToPrice(sqrtPriceX96));

    /// <summary>
    /// Deterministic decimal square root via Newton's method. A double seed starts the iteration;
    /// convergence and the final value are pure decimal, hence reproducible.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    public static decimal Sqrt(decimal value)
    {
        if (value < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Cannot take the square root of a negative number.");
        }

        if (value == 0m)
        {
            return 0m;
        }

        var guess = (decimal)Math.Sqrt((double)value);
        if (guess <= 0m)
        {
            guess = value;
        }

        // Newton iteration; quadratic convergence reaches a decimal fixed point well within this bound.
        for (var i = 0; i < 100; i++)
        {
            var next = (guess + value / guess) / 2m;
            if (next == guess)
            {
                break;
            }

            guess = next;
        }

        return guess;
    }

    private static void GuardTickRange(int tick)
    {
        if (tick < MinTick || tick > MaxTick)
        {
            throw new ArgumentOutOfRangeException(nameof(tick), tick, "Tick is outside the Uniswap v3 range.");
        }
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
