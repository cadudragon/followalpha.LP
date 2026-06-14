using FluentAssertions;
using FollowAlpha.LP.Domain.Primitives;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Primitives;

public class PriceTests
{
    [Fact]
    public void Constructor_defaults_to_the_canonical_orientation()
    {
        var price = new Price(2000m);
        price.Value.Should().Be(2000m);
        price.Orientation.Should().Be(PriceOrientation.Token1PerToken0);
        price.IsCanonical.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_rejects_non_positive_prices(decimal value)
    {
        var act = () => new Price(value);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Invert_flips_orientation_and_takes_the_reciprocal()
    {
        var price = new Price(2000m);

        var inverted = price.Invert();

        inverted.Orientation.Should().Be(PriceOrientation.Token0PerToken1);
        inverted.Value.Should().Be(1m / 2000m);
    }

    [Fact]
    public void To_canonical_is_a_no_op_for_a_canonical_price()
    {
        var price = new Price(2000m);
        price.ToCanonical().Should().Be(price);
    }

    [Fact]
    public void To_canonical_inverts_a_non_canonical_price()
    {
        var inverted = new Price(0.0005m, PriceOrientation.Token0PerToken1);

        var canonical = inverted.ToCanonical();

        canonical.Orientation.Should().Be(PriceOrientation.Token1PerToken0);
        canonical.Value.Should().Be(1m / 0.0005m);
    }

    [Fact]
    public void Orientation_does_not_silently_change_the_tick()
    {
        // The same physical price, expressed either way, must resolve to the same tick — the primitive
        // pins orientation rather than flipping floor<->ceiling (ARCHITECTURE.md §4.1, rule 2).
        var canonical = new Price(2000m);
        var inverted = canonical.Invert();

        inverted.ToTick().Value.Should().Be(canonical.ToTick().Value);
    }

    [Fact]
    public void To_tick_uses_floor_semantics()
    {
        var tick = new Price(2000m).ToTick();
        tick.ToPrice().Value.Should().BeLessThanOrEqualTo(2000m);
    }

    [Fact]
    public void To_sqrt_price_matches_the_math_core()
    {
        new Price(1m).ToSqrtPriceX96().Value.Should().Be(PriceMath.Q96);
    }
}
