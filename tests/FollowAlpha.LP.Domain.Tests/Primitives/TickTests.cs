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
    public void Min_and_max_expose_the_uniswap_bounds()
    {
        Tick.Min.Value.Should().Be(TickMath.MinTick);
        Tick.Max.Value.Should().Be(TickMath.MaxTick);
    }

    [Fact]
    public void To_sqrt_price_is_exact_at_tick_zero()
    {
        new Tick(0).ToSqrtPriceX96().Value.Should().Be(PriceMath.Q96);
    }

    [Theory]
    [InlineData(-100000)]
    [InlineData(-60)]
    [InlineData(0)]
    [InlineData(60)]
    [InlineData(100000)]
    public void Tick_round_trips_through_exact_sqrt_price(int tick)
    {
        new Tick(tick).ToSqrtPriceX96().ToTick().Value.Should().Be(tick);
    }

    [Fact]
    public void To_pool_price_returns_the_analytics_view()
    {
        new Tick(0).ToPoolPrice().RawToken1PerToken0.Should().Be(1m);
    }

    [Fact]
    public void Records_with_the_same_value_are_equal()
    {
        new Tick(42).Should().Be(new Tick(42));
    }
}
