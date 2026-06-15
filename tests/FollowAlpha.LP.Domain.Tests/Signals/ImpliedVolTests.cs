using FollowAlpha.LP.Domain.Signals;
using FluentAssertions;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Signals;

public class ImpliedVolTests
{
    [Fact]
    public void Implied_vol_matches_the_formula()
    {
        // 2 * 0.003 * sqrt(100/100) * sqrt(365) = 0.006 * 19.10497...
        ImpliedVolCalculator.Calculate(0.003m, 100m, 100m).Should().BeApproximately(0.1146298m, 1e-6m);
    }

    [Fact]
    public void Implied_vol_rises_with_volume()
    {
        var low = ImpliedVolCalculator.Calculate(0.003m, 100m, 100m);
        var high = ImpliedVolCalculator.Calculate(0.003m, 400m, 100m);
        high.Should().BeApproximately(low * 2m, 1e-9m); // sqrt(4) = 2x
    }

    [Fact]
    public void Zero_volume_is_zero_iv()
    {
        ImpliedVolCalculator.Calculate(0.003m, 0m, 100m).Should().Be(0m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_non_positive_tick_tvl(decimal tickTvl)
    {
        var act = () => ImpliedVolCalculator.Calculate(0.003m, 100m, tickTvl);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Rejects_negative_fee_or_volume()
    {
        var negFee = () => ImpliedVolCalculator.Calculate(-0.001m, 100m, 100m);
        var negVol = () => ImpliedVolCalculator.Calculate(0.003m, -1m, 100m);
        negFee.Should().Throw<ArgumentOutOfRangeException>();
        negVol.Should().Throw<ArgumentOutOfRangeException>();
    }
}
