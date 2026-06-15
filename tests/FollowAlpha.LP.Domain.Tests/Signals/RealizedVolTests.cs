using FollowAlpha.LP.Domain.Signals;
using FluentAssertions;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Signals;

public class RealizedVolTests
{
    [Fact]
    public void Constant_growth_has_zero_volatility()
    {
        // Equal log returns -> zero dispersion.
        RealizedVolEstimator.StdDevOfLogReturns([100m, 110m, 121m]).Should().BeApproximately(0m, 1e-12m);
    }

    [Fact]
    public void Std_dev_of_log_returns_matches_worked_example()
    {
        // returns ln(1.1), ln(0.9); sample stddev = sqrt(2)*|ln(1.1/0.9)/2| = 0.14189559...
        RealizedVolEstimator.StdDevOfLogReturns([100m, 110m, 99m]).Should().BeApproximately(0.1418956m, 1e-6m);
    }

    [Fact]
    public void Annualization_scales_by_sqrt_periods()
    {
        var perPeriod = RealizedVolEstimator.StdDevOfLogReturns([100m, 110m, 99m]);
        var annualized = RealizedVolEstimator.Annualized([100m, 110m, 99m], 4);
        annualized.Should().BeApproximately(perPeriod * 2m, 1e-9m); // sqrt(4) = 2
    }

    [Fact]
    public void Requires_at_least_three_prices()
    {
        var act = () => RealizedVolEstimator.StdDevOfLogReturns([100m, 110m]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rejects_non_positive_prices()
    {
        var act = () => RealizedVolEstimator.StdDevOfLogReturns([100m, 0m, 110m]);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Rejects_non_positive_periods_per_year()
    {
        var act = () => RealizedVolEstimator.Annualized([100m, 110m, 99m], 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
