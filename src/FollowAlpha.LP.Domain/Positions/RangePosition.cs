using System.Numerics;
using FollowAlpha.LP.Domain.Kernel;
using FollowAlpha.LP.Domain.Primitives;

namespace FollowAlpha.LP.Domain.Positions;

/// <summary>
/// A concentrated-liquidity position: a tick range, raw liquidity, and when it was opened. Valuation is
/// analytics-grade <see cref="decimal"/> in raw pool terms (token1 numeraire); the raw on-chain
/// <see cref="Liquidity"/> is converted to decimal exactly here — the single named boundary where raw L
/// meets the analytics kernel (ARCHITECTURE.md §4.2/§4.3).
/// </summary>
public sealed record RangePosition
{
    /// <summary>Constructs a position over [<paramref name="lowerTick"/>, <paramref name="upperTick"/>].</summary>
    /// <exception cref="ArgumentException">The lower tick is not strictly below the upper tick.</exception>
    public RangePosition(
        Tick lowerTick,
        Tick upperTick,
        Liquidity liquidity,
        DateTimeOffset openedAtUtc,
        FeeTier feeTier,
        TokenDecimals decimals)
    {
        if (lowerTick.Value >= upperTick.Value)
        {
            throw new ArgumentException("Lower tick must be strictly below the upper tick.", nameof(lowerTick));
        }

        if (openedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("OpenedAtUtc must be UTC (zero offset).", nameof(openedAtUtc));
        }

        LowerTick = lowerTick;
        UpperTick = upperTick;
        Liquidity = liquidity;
        OpenedAtUtc = openedAtUtc;
        FeeTier = feeTier;
        Decimals = decimals;
    }

    /// <summary>Lower tick of the range.</summary>
    public Tick LowerTick { get; }

    /// <summary>Upper tick of the range.</summary>
    public Tick UpperTick { get; }

    /// <summary>Raw on-chain liquidity L.</summary>
    public Liquidity Liquidity { get; }

    /// <summary>When the position was opened (UTC, supplied by the caller).</summary>
    public DateTimeOffset OpenedAtUtc { get; }

    /// <summary>The pool's fee tier.</summary>
    public FeeTier FeeTier { get; }

    /// <summary>The pool tokens' decimals.</summary>
    public TokenDecimals Decimals { get; }

    /// <summary>The lower bound as a raw pool price (token1/token0).</summary>
    public decimal LowerPoolPrice => PriceMath.TickToPoolPrice(LowerTick.Value);

    /// <summary>The upper bound as a raw pool price (token1/token0).</summary>
    public decimal UpperPoolPrice => PriceMath.TickToPoolPrice(UpperTick.Value);

    /// <summary>
    /// Liquidity L as an analytics-grade <see cref="decimal"/>. This is the named raw→analytics boundary;
    /// never compare this to the raw <see cref="Liquidity"/> directly.
    /// </summary>
    /// <exception cref="OverflowException">L is outside the decimal range.</exception>
    public decimal LiquidityAsDecimal => (decimal)Liquidity.Value;

    /// <summary>The position's holding and value at <paramref name="price"/> (token1 numeraire).</summary>
    public PositionValuation ValueAt(PoolPrice price)
    {
        var (sqrtLower, sqrtUpper) = SqrtBounds();
        var sqrtPrice = PriceMath.Sqrt(price.RawToken1PerToken0);
        var liquidity = LiquidityAsDecimal;

        var x = LiquidityMath.CalculateX(liquidity, sqrtPrice, sqrtLower, sqrtUpper);
        var y = LiquidityMath.CalculateY(liquidity, sqrtPrice, sqrtLower, sqrtUpper);
        return new PositionValuation(x, y, y + x * price.RawToken1PerToken0);
    }

    /// <summary>The sqrt prices of the range bounds (analytics decimal), used by valuation and benchmarks.</summary>
    internal (decimal SqrtLower, decimal SqrtUpper) SqrtBounds() =>
        (PriceMath.Sqrt(LowerPoolPrice), PriceMath.Sqrt(UpperPoolPrice));

    /// <summary>token1 deposited for a single-sided <see cref="Intent.Accumulate"/> position (<c>L·(√b−√a)</c>).</summary>
    internal decimal DepositedToken1()
    {
        var (sa, sb) = SqrtBounds();
        return LiquidityAsDecimal * (sb - sa);
    }

    /// <summary>token0 deposited for a single-sided <see cref="Intent.Distribute"/> position (<c>L·(√b−√a)/(√a·√b)</c>).</summary>
    internal decimal DepositedToken0()
    {
        var (sa, sb) = SqrtBounds();
        return LiquidityAsDecimal * (sb - sa) / (sa * sb);
    }
}
