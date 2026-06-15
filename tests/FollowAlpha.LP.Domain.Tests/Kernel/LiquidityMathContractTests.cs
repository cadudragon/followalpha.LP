using FluentAssertions;
using FollowAlpha.LP.Domain.Kernel;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Kernel;

/// <summary>
/// The kernel's invalid-input contract: square-root prices must be positive, amounts non-negative,
/// liquidity positive, and ranges strictly ordered. Guards fail loudly rather than returning nonsense
/// (ARCHITECTURE.md §4.2 hardening). Happy-path parity is covered by the golden tests.
/// </summary>
public class LiquidityMathContractTests
{
    // Representative valid sqrt-price range and liquidity for the negative cases.
    private const decimal Sa = 38m;
    private const decimal Sb = 50m;
    private const decimal Sp = 44m;
    private const decimal L = 36m;

    [Fact]
    public void Get_liquidity_rejects_negative_amounts()
    {
        var negX = () => LiquidityMath.GetLiquidity(-1m, 4m, Sp, Sa, Sb);
        var negY = () => LiquidityMath.GetLiquidity(1m, -4m, Sp, Sa, Sb);
        negX.Should().Throw<ArgumentOutOfRangeException>();
        negY.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Get_liquidity_rejects_non_positive_sqrt_price(decimal sqrtPrice)
    {
        var act = () => LiquidityMath.GetLiquidity(1m, 4m, sqrtPrice, Sa, Sb);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Get_liquidity_rejects_equal_or_inverted_range()
    {
        var equal = () => LiquidityMath.GetLiquidity(1m, 4m, Sp, 44m, 44m);
        var inverted = () => LiquidityMath.GetLiquidity(1m, 4m, Sp, Sb, Sa);
        equal.Should().Throw<ArgumentException>();
        inverted.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Get_liquidity0_rejects_negative_amount_and_bad_range()
    {
        var negX = () => LiquidityMath.GetLiquidity0(-1m, Sa, Sb);
        var badRange = () => LiquidityMath.GetLiquidity0(1m, Sb, Sa);
        negX.Should().Throw<ArgumentOutOfRangeException>();
        badRange.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Calculate_x_rejects_non_positive_liquidity(decimal liquidity)
    {
        var act = () => LiquidityMath.CalculateX(liquidity, Sp, Sa, Sb);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Calculate_a1_rejects_non_positive_liquidity()
    {
        var act = () => LiquidityMath.CalculateA1(0m, Sp, 4m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Calculate_b1_rejects_negative_amount()
    {
        var act = () => LiquidityMath.CalculateB1(L, Sp, -1m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Amount_deltas_rejects_non_positive_next_price_and_bad_range()
    {
        var badNext = () => LiquidityMath.AmountDeltas(L, Sp, 0m, Sa, Sb);
        var badRange = () => LiquidityMath.AmountDeltas(L, Sp, 46m, Sb, Sa);
        badNext.Should().Throw<ArgumentOutOfRangeException>();
        badRange.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Zero_amounts_are_accepted()
    {
        // Zero is a valid amount (a single-sided position); only negatives are rejected.
        var act = () => LiquidityMath.GetLiquidity(2m, 0m, 30m, Sa, Sb); // price below range -> token0 only
        act.Should().NotThrow();
    }
}
