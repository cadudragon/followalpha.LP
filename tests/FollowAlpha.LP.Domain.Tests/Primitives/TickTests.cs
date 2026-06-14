using FluentAssertions;
using FollowAlpha.LP.Domain.Primitives;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Primitives;

public class TickTests
{
    [Fact]
    public void Constructor_stores_the_value()
    {
        new Tick(123).Value.Should().Be(123);
    }

    [Theory]
    [InlineData(-887273)]
    [InlineData(887273)]
    public void Constructor_rejects_ticks_outside_the_uniswap_range(int value)
    {
        var act = () => new Tick(value);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Min_and_Max_expose_the_uniswap_bounds()
    {
        Tick.Min.Value.Should().Be(PriceMath.MinTick);
        Tick.Max.Value.Should().Be(PriceMath.MaxTick);
    }

    [Fact]
    public void To_price_returns_a_canonical_price()
    {
        var price = new Tick(0).ToPrice();
        price.Value.Should().Be(1m);
        price.Orientation.Should().Be(PriceOrientation.Token1PerToken0);
    }

    [Theory]
    [InlineData(-5000)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5000)]
    public void Tick_round_trips_through_price(int tick)
    {
        new Tick(tick).ToPrice().ToTick().Value.Should().Be(tick);
    }

    [Fact]
    public void To_sqrt_price_matches_the_math_core()
    {
        new Tick(0).ToSqrtPriceX96().Value.Should().Be(PriceMath.Q96);
    }

    [Fact]
    public void Records_with_the_same_value_are_equal()
    {
        new Tick(42).Should().Be(new Tick(42));
    }
}
