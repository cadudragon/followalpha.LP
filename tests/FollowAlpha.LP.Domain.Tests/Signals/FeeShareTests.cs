using FollowAlpha.LP.Domain.Signals;
using FluentAssertions;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Signals;

public class FeeShareTests
{
    [Fact]
    public void Estimates_share_daily_fees_and_while_in_range_apr()
    {
        // share = 100/1000 = 0.1; daily fees = 0.003 * 1e6 * 0.1 = 300; APR = 300*365/10000 = 10.95.
        var estimate = FeeShareEstimator.Estimate(
            feeFraction: 0.003m, dailyVolume: 1_000_000m, ownLiquidity: 100m, inRangeLiquidity: 1000m, positionValue: 10_000m);

        estimate.FeeShare.Should().Be(0.1m);
        estimate.ExpectedDailyFees.Should().Be(300m);
        estimate.FeeApr.Should().Be(10.95m);
    }

    [Fact]
    public void Adding_liquidity_dilutes_the_share()
    {
        var small = FeeShareEstimator.Estimate(0.003m, 1_000_000m, 100m, 1000m, 10_000m).FeeShare;
        var diluted = FeeShareEstimator.Estimate(0.003m, 1_000_000m, 100m, 2000m, 10_000m).FeeShare;
        diluted.Should().BeLessThan(small);
    }

    [Fact]
    public void Rejects_non_positive_in_range_liquidity()
    {
        var act = () => FeeShareEstimator.Estimate(0.003m, 1_000_000m, 100m, 0m, 10_000m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Rejects_own_liquidity_exceeding_in_range()
    {
        var act = () => FeeShareEstimator.Estimate(0.003m, 1_000_000m, 2000m, 1000m, 10_000m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Rejects_non_positive_position_value()
    {
        var act = () => FeeShareEstimator.Estimate(0.003m, 1_000_000m, 100m, 1000m, 0m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
