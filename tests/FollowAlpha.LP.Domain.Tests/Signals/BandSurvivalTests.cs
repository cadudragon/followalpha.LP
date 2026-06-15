using FollowAlpha.LP.Domain.Signals;
using FluentAssertions;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Signals;

public class BandSurvivalTests
{
    [Fact]
    public void Records_time_to_exit_for_each_overlapping_entry()
    {
        // prices 100,105,110,90 with +/-5% bands:
        //  start 0 [95,105]: 105 in (inclusive), 110 out -> 2 steps
        //  start 1 [99.75,110.25]: 110 in, 90 out -> 2 steps
        //  start 2 [104.5,115.5]: 90 out -> 1 step
        var survival = BandSurvivalEstimator.ForWidth([100m, 105m, 110m, 90m], 0.05m);

        survival.ObservedCount.Should().Be(3);
        survival.CensoredCount.Should().Be(0);
        survival.TimesToExit.Should().Equal(1, 2, 2);
        survival.Median().Should().Be(2m);
        survival.Quantile(0m).Should().Be(1m);
        survival.Quantile(1m).Should().Be(2m);
    }

    [Fact]
    public void Upper_bound_is_inclusive()
    {
        // 105 sits exactly on the +5% bound -> inside (no exit); 106 is outside -> exit.
        BandSurvivalEstimator.ForWidth([100m, 105m], 0.05m).ObservedCount.Should().Be(0);
        BandSurvivalEstimator.ForWidth([100m, 106m], 0.05m).TimesToExit.Should().Equal(1);
    }

    [Fact]
    public void Entries_that_never_exit_are_right_censored()
    {
        var survival = BandSurvivalEstimator.ForWidth([100m, 101m, 102m], 0.5m);

        survival.ObservedCount.Should().Be(0);
        survival.CensoredCount.Should().Be(2);

        var act = () => survival.Median();
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-0.1)]
    public void Rejects_width_outside_open_unit_interval(decimal width)
    {
        var act = () => BandSurvivalEstimator.ForWidth([100m, 105m], width);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Requires_at_least_two_prices()
    {
        var act = () => BandSurvivalEstimator.ForWidth([100m], 0.05m);
        act.Should().Throw<ArgumentException>();
    }
}
