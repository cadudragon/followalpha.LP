using FollowAlpha.LP.Domain.Ranges;
using FluentAssertions;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Ranges;

public class RangeVerdictTests
{
    private static readonly RangeVerdictPolicy Policy = new(minIvToForecastRatio: 1.0m, minNetExpectancy: 0m);

    [Fact]
    public void Opens_when_vol_is_sold_rich_and_expectancy_is_positive()
    {
        var inputs = new RangeVerdictInputs(poolImpliedVol: 1.0m, forecastVol: 0.5m, expectedFeesOverHorizon: 100m, expectedExitCost: 50m);

        var verdict = RangeVerdictCalculator.Evaluate(inputs, Policy);

        verdict.Verdict.Should().Be(Verdict.Open);
        verdict.NetExpectancy.Should().Be(50m);
        verdict.IvToForecastRatio.Should().Be(2m);
        verdict.ExpectancyPositive.Should().BeTrue();
        verdict.VolSoldRich.Should().BeTrue();
    }

    [Fact]
    public void Does_not_open_when_expectancy_is_negative()
    {
        var inputs = new RangeVerdictInputs(1.0m, 0.5m, expectedFeesOverHorizon: 40m, expectedExitCost: 50m);

        var verdict = RangeVerdictCalculator.Evaluate(inputs, Policy);

        verdict.Verdict.Should().Be(Verdict.DoNotOpen);
        verdict.ExpectancyPositive.Should().BeFalse();
        verdict.VolSoldRich.Should().BeTrue();
    }

    [Fact]
    public void Iv_gate_vetoes_regardless_of_a_strong_expectancy()
    {
        // Vol sold cheap (IV < forecast) -> DON'T OPEN even though fees crush the exit cost (§6b veto).
        var inputs = new RangeVerdictInputs(poolImpliedVol: 0.4m, forecastVol: 0.5m, expectedFeesOverHorizon: 1000m, expectedExitCost: 1m);

        var verdict = RangeVerdictCalculator.Evaluate(inputs, Policy);

        verdict.Verdict.Should().Be(Verdict.DoNotOpen);
        verdict.ExpectancyPositive.Should().BeTrue();
        verdict.VolSoldRich.Should().BeFalse();
    }

    [Fact]
    public void Gates_are_inclusive_at_the_thresholds()
    {
        // ratio exactly 1.0 and net expectancy exactly 0 both clear (>=).
        var inputs = new RangeVerdictInputs(0.5m, 0.5m, expectedFeesOverHorizon: 50m, expectedExitCost: 50m);

        var verdict = RangeVerdictCalculator.Evaluate(inputs, Policy);

        verdict.IvToForecastRatio.Should().Be(1m);
        verdict.NetExpectancy.Should().Be(0m);
        verdict.Verdict.Should().Be(Verdict.Open);
    }

    [Fact]
    public void Verdict_carries_the_full_snapshot_for_the_decision_log()
    {
        var inputs = new RangeVerdictInputs(1.0m, 0.5m, 100m, 50m);

        var verdict = RangeVerdictCalculator.Evaluate(inputs, Policy);

        verdict.Inputs.Should().Be(inputs);
        verdict.Policy.Should().Be(Policy);
    }

    [Fact]
    public void Inputs_reject_invalid_values()
    {
        var badForecast = () => new RangeVerdictInputs(1.0m, 0m, 1m, 1m);
        var negFees = () => new RangeVerdictInputs(1.0m, 0.5m, -1m, 1m);
        badForecast.Should().Throw<ArgumentOutOfRangeException>();
        negFees.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Policy_rejects_non_positive_ratio()
    {
        var act = () => new RangeVerdictPolicy(0m, 0m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
