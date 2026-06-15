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
    public void To_tick_is_exact_at_Q96()
    {
        new SqrtPriceX96(PriceMath.Q96).ToTick().Value.Should().Be(0);
    }

    [Fact]
    public void To_pool_price_returns_the_analytics_view()
    {
        new SqrtPriceX96(PriceMath.Q96).ToPoolPrice().RawToken1PerToken0.Should().Be(1m);
    }
}
