using FluentAssertions;
using FollowAlpha.LP.Domain.Primitives;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Primitives;

/// <summary>
/// Documents the behavior of <c>default(struct)</c> for the primitives. C# always allows the
/// parameterless default, bypassing constructor validation; these tests pin whether that default is a
/// usable zero value or a value whose conversions fail loudly (never silently wrong).
/// </summary>
public class DefaultStructTests
{
    [Fact]
    public void Default_tick_is_tick_zero_and_usable()
    {
        default(Tick).Value.Should().Be(0);
        default(Tick).ToSqrtPriceX96().Value.Should().Be(PriceMath.Q96);
        default(Tick).ToPoolPrice().RawToken1PerToken0.Should().Be(1m);
    }

    [Fact]
    public void Default_sqrt_price_is_invalid_and_conversions_throw()
    {
        default(SqrtPriceX96).Value.Should().Be(0);
        var toTick = () => default(SqrtPriceX96).ToTick();
        var toPrice = () => default(SqrtPriceX96).ToPoolPrice();
        toTick.Should().Throw<ArgumentOutOfRangeException>();
        toPrice.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Default_human_price_is_invalid_and_conversions_throw()
    {
        default(HumanPrice).Value.Should().Be(0);
        var act = () => default(HumanPrice).ToPoolPrice(new TokenDecimals(18, 18));
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Default_pool_price_is_invalid_and_conversions_throw()
    {
        default(PoolPrice).RawToken1PerToken0.Should().Be(0);
        var act = () => default(PoolPrice).ToTick();
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Default_fee_tier_has_no_tick_spacing()
    {
        default(FeeTier).FeePips.Should().Be(0);
        var act = () => default(FeeTier).TickSpacing;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Default_token_decimals_is_zero_zero_and_valid()
    {
        default(TokenDecimals).Token0.Should().Be(0);
        default(TokenDecimals).Token1.Should().Be(0);
    }

    [Fact]
    public void Default_token_amount_is_zero_and_usable()
    {
        default(TokenAmount).Raw.Should().Be(0);
        default(TokenAmount).ToDecimal().Should().Be(0m);
    }

    [Fact]
    public void Default_liquidity_is_zero()
    {
        default(Liquidity).Value.Should().Be(0);
        default(Liquidity).Should().Be(Liquidity.Zero);
    }
}
