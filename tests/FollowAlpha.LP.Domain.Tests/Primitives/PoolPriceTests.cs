using FluentAssertions;
using FollowAlpha.LP.Domain.Primitives;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Primitives;

public class PoolPriceTests
{
    [Fact]
    public void Constructor_stores_the_raw_ratio()
    {
        new PoolPrice(500_000_000m).RawToken1PerToken0.Should().Be(500_000_000m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_rejects_non_positive_prices(decimal value)
    {
        var act = () => new PoolPrice(value);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void To_tick_uses_floor_semantics()
    {
        var tick = new PoolPrice(500_000_000m).ToTick();
        tick.ToPoolPrice().RawToken1PerToken0.Should().BeLessThanOrEqualTo(500_000_000m);
        new Tick(tick.Value + 1).ToPoolPrice().RawToken1PerToken0.Should().BeGreaterThan(500_000_000m);
    }

    [Fact]
    public void To_human_price_in_inverted_orientation_is_the_reciprocal()
    {
        var decimals = new TokenDecimals(6, 18);
        var pool = new PoolPrice(500_000_000m);

        var inverted = pool.ToHumanPrice(decimals, PriceOrientation.Token0PerToken1);

        inverted.Orientation.Should().Be(PriceOrientation.Token0PerToken1);
        inverted.Value.Should().Be(1m / 0.0005m); // 2000 USDC per WETH
    }
}
