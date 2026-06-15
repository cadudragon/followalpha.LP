using FluentAssertions;
using FollowAlpha.LP.Domain.Primitives;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Primitives;

public class HumanPriceTests
{
    [Fact]
    public void Constructor_defaults_to_the_canonical_orientation()
    {
        var price = new HumanPrice(2000m);
        price.Value.Should().Be(2000m);
        price.Orientation.Should().Be(PriceOrientation.Token1PerToken0);
        price.IsCanonical.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_rejects_non_positive_prices(decimal value)
    {
        var act = () => new HumanPrice(value);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Invert_flips_orientation_and_takes_the_reciprocal()
    {
        var inverted = new HumanPrice(2000m).Invert();
        inverted.Orientation.Should().Be(PriceOrientation.Token0PerToken1);
        inverted.Value.Should().Be(1m / 2000m);
    }

    [Fact]
    public void To_canonical_inverts_a_non_canonical_price()
    {
        var canonical = new HumanPrice(0.0005m, PriceOrientation.Token0PerToken1).ToCanonical();
        canonical.Orientation.Should().Be(PriceOrientation.Token1PerToken0);
        canonical.Value.Should().Be(1m / 0.0005m);
    }

    [Fact]
    public void To_pool_price_applies_decimal_scaling_USDC_WETH()
    {
        // token0 = USDC (6), token1 = WETH (18). Human 0.0005 WETH per USDC (canonical) -> raw 5e8.
        var decimals = new TokenDecimals(6, 18);
        var human = new HumanPrice(0.0005m, PriceOrientation.Token1PerToken0);

        human.ToPoolPrice(decimals).RawToken1PerToken0.Should().Be(500_000_000m);
    }

    [Fact]
    public void Inverted_human_price_scales_to_the_same_pool_price()
    {
        // "2000 USDC per WETH" is token0/token1 — must land on the same raw pool price as 0.0005 canonical.
        var decimals = new TokenDecimals(6, 18);
        var inverted = new HumanPrice(2000m, PriceOrientation.Token0PerToken1);

        inverted.ToPoolPrice(decimals).RawToken1PerToken0.Should().Be(500_000_000m);
    }

    [Fact]
    public void Equal_decimals_make_pool_price_equal_to_the_canonical_human_value()
    {
        var decimals = new TokenDecimals(18, 18);
        new HumanPrice(2000m).ToPoolPrice(decimals).RawToken1PerToken0.Should().Be(2000m);
    }

    [Fact]
    public void To_tick_goes_through_decimal_scaling()
    {
        var decimals = new TokenDecimals(6, 18);
        var human = new HumanPrice(0.0005m);

        human.ToTick(decimals).Should().Be(human.ToPoolPrice(decimals).ToTick());
    }

    [Fact]
    public void Pool_price_round_trips_back_to_a_human_price()
    {
        var decimals = new TokenDecimals(6, 18);
        var human = new HumanPrice(0.0005m);

        var roundTripped = human.ToPoolPrice(decimals).ToHumanPrice(decimals);

        roundTripped.Orientation.Should().Be(PriceOrientation.Token1PerToken0);
        roundTripped.Value.Should().Be(0.0005m);
    }
}
