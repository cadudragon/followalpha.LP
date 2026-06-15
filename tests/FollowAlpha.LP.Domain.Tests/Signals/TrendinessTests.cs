using FollowAlpha.LP.Domain.Signals;
using FluentAssertions;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Signals;

public class TrendinessTests
{
    [Fact]
    public void Straight_trend_is_fully_efficient()
    {
        TrendinessEstimator.PathEfficiency([100m, 101m, 102m, 103m]).Should().Be(1m);
    }

    [Fact]
    public void Efficiency_is_direction_agnostic()
    {
        // A straight move down is just as "efficient" as up — no direction is emitted.
        TrendinessEstimator.PathEfficiency([100m, 90m, 80m]).Should().Be(1m);
    }

    [Fact]
    public void Round_trip_is_inefficient()
    {
        // net = 1, path = 3 -> 1/3.
        TrendinessEstimator.PathEfficiency([100m, 101m, 100m, 101m]).Should().BeApproximately(1m / 3m, 1e-12m);
    }

    [Fact]
    public void Flat_series_is_zero()
    {
        TrendinessEstimator.PathEfficiency([100m, 100m, 100m]).Should().Be(0m);
    }

    [Fact]
    public void Requires_at_least_two_prices()
    {
        var act = () => TrendinessEstimator.PathEfficiency([100m]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rejects_non_positive_prices()
    {
        var zero = () => TrendinessEstimator.PathEfficiency([100m, 0m, 50m]);
        var negative = () => TrendinessEstimator.PathEfficiency([100m, -1m]);
        zero.Should().Throw<ArgumentOutOfRangeException>();
        negative.Should().Throw<ArgumentOutOfRangeException>();
    }
}
