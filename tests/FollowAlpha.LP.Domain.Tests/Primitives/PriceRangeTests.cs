using FluentAssertions;
using FollowAlpha.LP.Domain.Primitives;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Primitives;

public class PriceRangeTests
{
    [Fact]
    public void Constructor_stores_ordered_bounds()
    {
        var range = new PriceRange(new Price(1500m), new Price(2500m));
        range.Lower.Value.Should().Be(1500m);
        range.Upper.Value.Should().Be(2500m);
    }

    [Fact]
    public void Constructor_rejects_unordered_bounds()
    {
        var act = () => new PriceRange(new Price(2500m), new Price(1500m));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_rejects_mixed_orientations()
    {
        var act = () => new PriceRange(
            new Price(1500m, PriceOrientation.Token1PerToken0),
            new Price(2500m, PriceOrientation.Token0PerToken1));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void To_initialized_ticks_contains_the_request_and_aligns_to_spacing()
    {
        var feeTier = FeeTier.Medium; // spacing 60
        var range = new PriceRange(new Price(1500m), new Price(2500m));

        var (lower, upper) = range.ToInitializedTicks(feeTier);

        // Aligned to tick spacing.
        (lower.Value % feeTier.TickSpacing).Should().Be(0);
        (upper.Value % feeTier.TickSpacing).Should().Be(0);

        // Contains the request: the band's prices bracket the requested bounds, never narrower.
        PriceMath.TickToPrice(lower.Value).Should().BeLessThanOrEqualTo(1500m);
        PriceMath.TickToPrice(upper.Value).Should().BeGreaterThanOrEqualTo(2500m);
    }

    [Fact]
    public void Inverted_orientation_maps_to_the_same_ticks_with_bounds_swapped()
    {
        var feeTier = FeeTier.Medium;
        var canonical = new PriceRange(new Price(1500m), new Price(2500m));

        // Same physical band, expressed in the inverse orientation: reciprocation reverses order, so the
        // lower inverted price is 1/2500 and the upper is 1/1500.
        var inverted = new PriceRange(
            new Price(1m / 2500m, PriceOrientation.Token0PerToken1),
            new Price(1m / 1500m, PriceOrientation.Token0PerToken1));

        inverted.ToInitializedTicks(feeTier).Should().Be(canonical.ToInitializedTicks(feeTier));
    }
}
