using FluentAssertions;
using FollowAlpha.LP.Domain.Primitives;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Primitives;

public class FeeTierTests
{
    [Theory]
    [InlineData(100, 1)]
    [InlineData(500, 10)]
    [InlineData(3000, 60)]
    [InlineData(10000, 200)]
    public void Tick_spacing_map_matches_the_contract(int feePips, int spacing)
    {
        FeeTier.FromPips(feePips).TickSpacing.Should().Be(spacing);
    }

    [Fact]
    public void Canonical_tiers_expose_their_pips()
    {
        FeeTier.Stable.FeePips.Should().Be(100);
        FeeTier.Low.FeePips.Should().Be(500);
        FeeTier.Medium.FeePips.Should().Be(3000);
        FeeTier.High.FeePips.Should().Be(10000);
    }

    [Theory]
    [InlineData(100, 0.0001)]
    [InlineData(500, 0.0005)]
    [InlineData(3000, 0.003)]
    [InlineData(10000, 0.01)]
    public void Fee_fraction_is_pips_over_a_million(int feePips, decimal expected)
    {
        FeeTier.FromPips(feePips).FeeFraction.Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(250)]
    [InlineData(99)]
    [InlineData(10001)]
    public void From_pips_rejects_non_canonical_tiers(int feePips)
    {
        var act = () => FeeTier.FromPips(feePips);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Default_instance_has_no_tick_spacing()
    {
        var act = () => default(FeeTier).TickSpacing;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Tiers_with_the_same_pips_are_equal()
    {
        FeeTier.FromPips(500).Should().Be(FeeTier.Low);
    }
}
