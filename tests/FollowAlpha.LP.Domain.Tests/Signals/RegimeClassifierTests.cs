using FollowAlpha.LP.Domain.Signals;
using FluentAssertions;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Signals;

public class RegimeClassifierTests
{
    private static readonly RegimePolicy Policy = RegimePolicy.Default;

    [Fact]
    public void Has_enough_data_tracks_the_min_bars_threshold()
    {
        RegimeClassifier.HasEnoughData(Policy.MinBars - 1, Policy).Should().BeFalse();
        RegimeClassifier.HasEnoughData(Policy.MinBars, Policy).Should().BeTrue();
    }

    [Fact]
    public void Classify_throws_when_history_is_too_thin()
    {
        var prices = Enumerable.Repeat(100m, Policy.MinBars - 1).ToList();

        var act = () => RegimeClassifier.Classify(prices, Policy);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Tight_oscillation_is_classified_range()
    {
        // Choppy (low path-efficiency) and vol not elevated vs its own history.
        var prices = Series(45, i => i % 2 == 0 ? 100m : 100.5m);

        var result = RegimeClassifier.Classify(prices, Policy);

        result.Regime.Should().Be(Regime.Range);
        result.Evidence.Trendiness.Should().BeLessThan(Policy.TrendinessCutoff);
        result.Evidence.RvPercentile.Should().BeLessThanOrEqualTo(Policy.RangeRvPercentileMax);
    }

    [Fact]
    public void Monotonic_accelerating_uptrend_is_classified_trending()
    {
        // Strictly increasing => path-efficiency 1.0; widening steps => the most recent RV window is the
        // highest => percentile high. Direction is never reported, only the regime label.
        var price = 100m;
        var prices = new List<decimal> { price };
        for (var i = 1; i < 45; i++)
        {
            price *= 1m + (0.001m * i);
            prices.Add(price);
        }

        var result = RegimeClassifier.Classify(prices, Policy);

        result.Regime.Should().Be(Regime.Trending);
        result.Evidence.Trendiness.Should().BeGreaterThanOrEqualTo(Policy.TrendinessCutoff);
    }

    [Fact]
    public void Choppy_with_a_recent_vol_spike_is_classified_transition()
    {
        // Low trendiness (choppy) but elevated recent vol => neither Range nor Trending.
        var prices = new List<decimal>();
        for (var i = 0; i < 30; i++)
        {
            prices.Add(i % 2 == 0 ? 100m : 100.2m);
        }

        for (var i = 0; i < 15; i++)
        {
            prices.Add(i % 2 == 0 ? 100m : 108m);
        }

        var result = RegimeClassifier.Classify(prices, Policy);

        result.Regime.Should().Be(Regime.Transition);
        result.Evidence.Trendiness.Should().BeLessThan(Policy.TrendinessCutoff);
        result.Evidence.RvPercentile.Should().BeGreaterThan(Policy.RangeRvPercentileMax);
    }

    [Fact]
    public void Evidence_is_populated_and_within_bounds()
    {
        var prices = Series(45, i => 100m + (i % 3));

        var ev = RegimeClassifier.Classify(prices, Policy).Evidence;

        ev.SampleCount.Should().Be(45);
        ev.RvWindow.Should().Be(Policy.RvWindow);
        ev.PercentileLookback.Should().Be(Policy.PercentileLookback);
        ev.TrendinessWindow.Should().Be(Policy.TrendinessWindow);
        ev.MinBars.Should().Be(Policy.MinBars);
        ev.RvPercentile.Should().BeInRange(0m, 100m);
        ev.Trendiness.Should().BeInRange(0m, 1m);
        ev.ClassificationReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Classification_is_deterministic()
    {
        var prices = Series(50, i => 100m + (i % 5));

        var a = RegimeClassifier.Classify(prices, Policy);
        var b = RegimeClassifier.Classify(prices, Policy);

        a.Should().BeEquivalentTo(b);
    }

    private static List<decimal> Series(int count, Func<int, decimal> f) =>
        Enumerable.Range(0, count).Select(f).ToList();
}
