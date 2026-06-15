using System.Numerics;
using FollowAlpha.LP.Domain.Kernel;
using FollowAlpha.LP.Domain.Positions;
using FollowAlpha.LP.Domain.Primitives;
using FluentAssertions;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Positions;

public class RangePositionTests
{
    private static readonly DateTimeOffset Opened = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static RangePosition Position() => new(
        new Tick(-1000),
        new Tick(1000),
        new Liquidity(new BigInteger(1_000_000)),
        Opened,
        FeeTier.Medium,
        new TokenDecimals(18, 18));

    [Fact]
    public void Constructor_rejects_inverted_range()
    {
        var act = () => new RangePosition(
            new Tick(1000), new Tick(-1000), new Liquidity(BigInteger.One), Opened, FeeTier.Medium, new TokenDecimals(18, 18));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Below_range_the_position_is_all_token0()
    {
        var v = Position().ValueAt(new PoolPrice(0.5m)); // below lower bound (~0.905)
        v.AmountY.Should().Be(0m);
        v.AmountX.Should().BeGreaterThan(0m);
        v.Value.Should().Be(v.AmountY + v.AmountX * 0.5m);
    }

    [Fact]
    public void Above_range_the_position_is_all_token1()
    {
        var v = Position().ValueAt(new PoolPrice(2.0m)); // above upper bound (~1.105)
        v.AmountX.Should().Be(0m);
        v.AmountY.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void In_range_the_position_holds_both_tokens()
    {
        var v = Position().ValueAt(new PoolPrice(1.0m));
        v.AmountX.Should().BeGreaterThan(0m);
        v.AmountY.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void Value_increases_with_price()
    {
        var p = Position();
        var low = p.ValueAt(new PoolPrice(0.5m)).Value;
        var mid = p.ValueAt(new PoolPrice(1.0m)).Value;
        var high = p.ValueAt(new PoolPrice(2.0m)).Value;
        low.Should().BeLessThan(mid);
        mid.Should().BeLessThan(high);
    }

    [Fact]
    public void Value_at_wires_the_kernel_with_the_range_sqrt_bounds()
    {
        var p = Position();
        var sa = PriceMath.Sqrt(p.LowerPoolPrice);
        var sb = PriceMath.Sqrt(p.UpperPoolPrice);
        var sp = PriceMath.Sqrt(1.0m);
        var liquidity = p.LiquidityAsDecimal;

        var v = p.ValueAt(new PoolPrice(1.0m));

        v.AmountX.Should().Be(LiquidityMath.CalculateX(liquidity, sp, sa, sb));
        v.AmountY.Should().Be(LiquidityMath.CalculateY(liquidity, sp, sa, sb));
    }

    [Fact]
    public void Deposited_single_sided_amounts_match_the_liquidity_geometry()
    {
        var p = Position();
        var sa = PriceMath.Sqrt(p.LowerPoolPrice);
        var sb = PriceMath.Sqrt(p.UpperPoolPrice);
        var l = p.LiquidityAsDecimal;

        // Accumulate deposits token1 = L*(sb-sa); Distribute deposits token0 = L*(sb-sa)/(sa*sb).
        LimitOrderBenchmark.Accumulate(p, LimitLadder.UniformQuoteByPrice).Budget
            .Should().BeApproximately(l * (sb - sa), 1e-6m);
        LimitOrderBenchmark.Distribute(p, LimitLadder.UniformQuoteByPrice).Budget
            .Should().BeApproximately(l * (sb - sa) / (sa * sb), 1e-6m);
    }
}
