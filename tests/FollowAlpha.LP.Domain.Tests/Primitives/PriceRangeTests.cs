using FluentAssertions;
using FollowAlpha.LP.Domain.Primitives;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Primitives;

public class PriceRangeTests
{
    private static readonly TokenDecimals UsdcWeth = new(6, 18);

    [Fact]
    public void Constructor_stores_ordered_bounds()
    {
        var range = new PriceRange(new HumanPrice(1500m), new HumanPrice(2500m));
        range.Lower.Value.Should().Be(1500m);
        range.Upper.Value.Should().Be(2500m);
    }

    [Fact]
    public void Constructor_rejects_unordered_bounds()
    {
        var act = () => new PriceRange(new HumanPrice(2500m), new HumanPrice(1500m));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_rejects_mixed_orientations()
    {
        var act = () => new PriceRange(
            new HumanPrice(1500m, PriceOrientation.Token1PerToken0),
            new HumanPrice(2500m, PriceOrientation.Token0PerToken1));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void To_initialized_ticks_contains_the_request_and_aligns_to_spacing()
    {
        var feeTier = FeeTier.Medium; // spacing 60
        // Canonical token1/token0 band (decimals equal so raw == human, keeps the assertion direct).
        var range = new PriceRange(new HumanPrice(1500m), new HumanPrice(2500m));
        var decimals = new TokenDecimals(18, 18);

        var (lower, upper) = range.ToInitializedTicks(feeTier, decimals);

        (lower.Value % feeTier.TickSpacing).Should().Be(0);
        (upper.Value % feeTier.TickSpacing).Should().Be(0);

        // Contains the request in raw pool-price space, never narrower.
        PriceMath.TickToPoolPrice(lower.Value).Should().BeLessThanOrEqualTo(1500m);
        PriceMath.TickToPoolPrice(upper.Value).Should().BeGreaterThanOrEqualTo(2500m);
    }

    [Fact]
    public void Scaling_is_applied_before_mapping_to_ticks()
    {
        // With real USDC/WETH decimals, a human band must contain the request in the *scaled* raw space.
        var feeTier = FeeTier.Low; // spacing 10
        var range = new PriceRange(new HumanPrice(0.0004m), new HumanPrice(0.0006m));

        var (lower, upper) = range.ToInitializedTicks(feeTier, UsdcWeth);

        var rawLower = new HumanPrice(0.0004m).ToPoolPrice(UsdcWeth).RawToken1PerToken0;
        var rawUpper = new HumanPrice(0.0006m).ToPoolPrice(UsdcWeth).RawToken1PerToken0;

        (lower.Value % feeTier.TickSpacing).Should().Be(0);
        (upper.Value % feeTier.TickSpacing).Should().Be(0);
        PriceMath.TickToPoolPrice(lower.Value).Should().BeLessThanOrEqualTo(rawLower);
        PriceMath.TickToPoolPrice(upper.Value).Should().BeGreaterThanOrEqualTo(rawUpper);
    }

    [Fact]
    public void Inverted_orientation_maps_to_the_same_ticks_with_bounds_swapped()
    {
        var feeTier = FeeTier.Medium;
        var canonical = new PriceRange(new HumanPrice(0.0004m), new HumanPrice(0.0006m));

        // Same physical band in token0/token1 terms: reciprocation reverses order.
        var inverted = new PriceRange(
            new HumanPrice(1m / 0.0006m, PriceOrientation.Token0PerToken1),
            new HumanPrice(1m / 0.0004m, PriceOrientation.Token0PerToken1));

        inverted.ToInitializedTicks(feeTier, UsdcWeth)
            .Should().Be(canonical.ToInitializedTicks(feeTier, UsdcWeth));
    }
}
