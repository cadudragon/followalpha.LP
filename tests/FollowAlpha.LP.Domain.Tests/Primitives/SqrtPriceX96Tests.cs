using System.Numerics;
using FluentAssertions;
using FollowAlpha.LP.Domain.Primitives;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Primitives;

public class SqrtPriceX96Tests
{
    [Fact]
    public void Constructor_stores_the_raw_value()
    {
        new SqrtPriceX96(PriceMath.Q96).Value.Should().Be(PriceMath.Q96);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_rejects_non_positive_values(int value)
    {
        var act = () => new SqrtPriceX96(new BigInteger(value));
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void To_price_returns_a_canonical_price()
    {
        var price = new SqrtPriceX96(PriceMath.Q96).ToPrice();
        price.Value.Should().Be(1m);
        price.Orientation.Should().Be(PriceOrientation.Token1PerToken0);
    }

    [Fact]
    public void To_tick_floors_to_the_implied_tick()
    {
        new SqrtPriceX96(PriceMath.Q96).ToTick().Value.Should().Be(0);
    }
}
