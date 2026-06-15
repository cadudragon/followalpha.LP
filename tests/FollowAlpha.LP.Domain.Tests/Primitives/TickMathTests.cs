using System.Globalization;
using System.Numerics;
using FluentAssertions;
using FollowAlpha.LP.Domain.Primitives;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Primitives;

public class TickMathTests
{
    // Published Uniswap v3-core constants — independent ground truth.
    private static readonly BigInteger Q96 = BigInteger.Parse("79228162514264337593543950336", CultureInfo.InvariantCulture);
    private static readonly BigInteger MinSqrtRatio = BigInteger.Parse("4295128739", CultureInfo.InvariantCulture);
    private static readonly BigInteger MaxSqrtRatio =
        BigInteger.Parse("1461446703485210103287273052203988822378723970342", CultureInfo.InvariantCulture);

    [Fact]
    public void Tick_zero_is_Q96()
    {
        TickMath.GetSqrtRatioAtTick(0).Should().Be(Q96);
    }

    [Fact]
    public void Min_tick_is_min_sqrt_ratio()
    {
        // Exercises every magic constant and the full negative-tick chain.
        TickMath.GetSqrtRatioAtTick(TickMath.MinTick).Should().Be(MinSqrtRatio);
    }

    [Fact]
    public void Max_tick_is_max_sqrt_ratio()
    {
        // Exercises every magic constant plus the positive-tick inversion + round-up downcast.
        TickMath.GetSqrtRatioAtTick(TickMath.MaxTick).Should().Be(MaxSqrtRatio);
    }

    [Fact]
    public void Published_min_max_sqrt_ratio_fields_match()
    {
        TickMath.MinSqrtRatio.Should().Be(MinSqrtRatio);
        TickMath.MaxSqrtRatio.Should().Be(MaxSqrtRatio);
    }

    [Theory]
    [InlineData(-887271)]
    [InlineData(-100000)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100000)]
    [InlineData(887271)]
    public void Sqrt_ratio_is_strictly_increasing(int tick)
    {
        TickMath.GetSqrtRatioAtTick(tick + 1).Should().BeGreaterThan(TickMath.GetSqrtRatioAtTick(tick));
    }

    [Theory]
    [InlineData(-887272)]
    [InlineData(-100000)]
    [InlineData(-60)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(100000)]
    [InlineData(887271)]
    public void Tick_round_trips_through_sqrt_ratio(int tick)
    {
        // getTickAtSqrtRatio is defined as the greatest tick whose ratio <= input, so the round trip
        // of an exact ratio is the identity. (MaxTick excluded: its ratio == MaxSqrtRatio is out of range.)
        TickMath.GetTickAtSqrtRatio(TickMath.GetSqrtRatioAtTick(tick)).Should().Be(tick);
    }

    [Fact]
    public void Get_tick_floors_between_boundaries()
    {
        var ratioAt100 = TickMath.GetSqrtRatioAtTick(100);
        var ratioAt101 = TickMath.GetSqrtRatioAtTick(101);
        var between = ratioAt100 + (ratioAt101 - ratioAt100) / 2;

        TickMath.GetTickAtSqrtRatio(between).Should().Be(100);
    }

    [Theory]
    [InlineData(-887273)]
    [InlineData(887273)]
    public void Get_sqrt_ratio_rejects_out_of_range_ticks(int tick)
    {
        var act = () => TickMath.GetSqrtRatioAtTick(tick);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Get_tick_rejects_ratios_outside_the_valid_range()
    {
        var below = () => TickMath.GetTickAtSqrtRatio(MinSqrtRatio - 1);
        var atMax = () => TickMath.GetTickAtSqrtRatio(MaxSqrtRatio);
        below.Should().Throw<ArgumentOutOfRangeException>();
        atMax.Should().Throw<ArgumentOutOfRangeException>();
    }
}
