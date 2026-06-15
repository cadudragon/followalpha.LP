using System.Globalization;
using System.Numerics;

namespace FollowAlpha.LP.Domain.Primitives;

/// <summary>
/// Exact integer port of Uniswap v3-core <c>TickMath</c> (the canonical engine for
/// <c>Tick ↔ SqrtPriceX96</c>). <c>sqrtPriceX96</c> is raw on-chain data, so this matches Uniswap
/// bit-for-bit — including the round-<b>up</b> of the Q128.128→Q64.96 downcast — rather than
/// approximating with <see cref="decimal"/> arithmetic. Pure <see cref="BigInteger"/> integer math,
/// hence deterministic across platforms (NFR D1) and BCL-only.
///
/// <para>Source of truth: the vendored v3-core <c>contracts/libraries/TickMath.sol</c> under
/// <c>C:\SRC\reference_libs\Nethereum.Uniswap</c>. Validated against its published constants
/// (<see cref="MinSqrtRatio"/>, <see cref="MaxSqrtRatio"/>, and <c>GetSqrtRatioAtTick(0) = 2^96</c>),
/// which exercise every magic constant and the positive-tick inversion. (That repo's managed
/// <c>V4TickMath</c> is scale-buggy — <c>GetSqrtRatioAtTick(0)</c> returns 2^64 — so it is not used.)</para>
/// </summary>
public static class TickMath
{
    /// <summary>Minimum tick (v3-core <c>MIN_TICK</c>).</summary>
    public const int MinTick = -887272;

    /// <summary>Maximum tick (v3-core <c>MAX_TICK</c>).</summary>
    public const int MaxTick = 887272;

    /// <summary><c>GetSqrtRatioAtTick(MinTick)</c> — the lowest value the function can return.</summary>
    public static readonly BigInteger MinSqrtRatio = BigInteger.Parse("4295128739", CultureInfo.InvariantCulture);

    /// <summary><c>GetSqrtRatioAtTick(MaxTick)</c> — the highest value the function can return.</summary>
    public static readonly BigInteger MaxSqrtRatio =
        BigInteger.Parse("1461446703485210103287273052203988822378723970342", CultureInfo.InvariantCulture);

    private static readonly BigInteger Q32 = BigInteger.One << 32;
    private static readonly BigInteger UInt256Max = (BigInteger.One << 256) - BigInteger.One;

    // Multiplier for the lowest bit of |tick| (bit 0x1).
    private static readonly BigInteger Bit0Multiplier = Hex("fffcb933bd6fad37aa2d162d1a594001");

    // Multipliers for bits 0x2 .. 0x80000, in ascending bit order (index k → bit 1<<(k+1)).
    private static readonly BigInteger[] BitMultipliers =
    [
        Hex("fff97272373d413259a46990580e213a"), // 0x2
        Hex("fff2e50f5f656932ef12357cf3c7fdcc"), // 0x4
        Hex("ffe5caca7e10e4e61c3624eaa0941cd0"), // 0x8
        Hex("ffcb9843d60f6159c9db58835c926644"), // 0x10
        Hex("ff973b41fa98c081472e6896dfb254c0"), // 0x20
        Hex("ff2ea16466c96a3843ec78b326b52861"), // 0x40
        Hex("fe5dee046a99a2a811c461f1969c3053"), // 0x80
        Hex("fcbe86c7900a88aedcffc83b479aa3a4"), // 0x100
        Hex("f987a7253ac413176f2b074cf7815e54"), // 0x200
        Hex("f3392b0822b70005940c7a398e4b70f3"), // 0x400
        Hex("e7159475a2c29b7443b29c7fa6e889d9"), // 0x800
        Hex("d097f3bdfd2022b8845ad8f792aa5825"), // 0x1000
        Hex("a9f746462d870fdf8a65dc1f90e061e5"), // 0x2000
        Hex("70d869a156d2a1b890bb3df62baf32f7"), // 0x4000
        Hex("31be135f97d08fd981231505542fcfa6"), // 0x8000
        Hex("9aa508b5b7a84e1c677de54f3e99bc9"),  // 0x10000
        Hex("5d6af8dedb81196699c329225ee604"),   // 0x20000
        Hex("2216e584f5fa1ea926041bedfe98"),     // 0x40000
        Hex("48a170391f7dc42444e8fa2"),          // 0x80000
    ];

    /// <summary><c>sqrt(1.0001^tick) · 2^96</c> as a Q64.96 integer, exactly per v3-core.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> is outside <see cref="MinTick"/>..<see cref="MaxTick"/>.</exception>
    public static BigInteger GetSqrtRatioAtTick(int tick)
    {
        if (tick < MinTick || tick > MaxTick)
        {
            throw new ArgumentOutOfRangeException(nameof(tick), tick, "Tick is outside the Uniswap v3 range.");
        }

        var absTick = tick < 0 ? -(long)tick : tick;

        var ratio = (absTick & 0x1) != 0 ? Bit0Multiplier : BigInteger.One << 128;

        for (var k = 1; k <= 19; k++)
        {
            if ((absTick & (1L << k)) != 0)
            {
                ratio = ratio * BitMultipliers[k - 1] >> 128;
            }
        }

        if (tick > 0)
        {
            ratio = UInt256Max / ratio;
        }

        // Q128.128 → Q64.96, rounding up so GetTickAtSqrtRatio of the output is always consistent.
        return (ratio >> 32) + (ratio % Q32 == BigInteger.Zero ? BigInteger.Zero : BigInteger.One);
    }

    /// <summary>
    /// The greatest tick whose ratio is ≤ <paramref name="sqrtPriceX96"/> (v3-core
    /// <c>getTickAtSqrtRatio</c> definition). Implemented as an exact binary search over
    /// <see cref="GetSqrtRatioAtTick"/>, which is strictly increasing across the whole tick range, so
    /// the result is identical to the on-chain function and fully deterministic.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sqrtPriceX96"/> is outside <c>[MinSqrtRatio, MaxSqrtRatio)</c>.</exception>
    public static int GetTickAtSqrtRatio(BigInteger sqrtPriceX96)
    {
        if (sqrtPriceX96 < MinSqrtRatio || sqrtPriceX96 >= MaxSqrtRatio)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sqrtPriceX96),
                "sqrtPriceX96 is outside the valid range [MinSqrtRatio, MaxSqrtRatio).");
        }

        var lo = MinTick;
        var hi = MaxTick;
        while (lo < hi)
        {
            var mid = lo + ((hi - lo + 1) / 2); // upper mid: find the last tick whose ratio ≤ input
            if (GetSqrtRatioAtTick(mid) <= sqrtPriceX96)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return lo;
    }

    private static BigInteger Hex(string hex) =>
        BigInteger.Parse("0" + hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
