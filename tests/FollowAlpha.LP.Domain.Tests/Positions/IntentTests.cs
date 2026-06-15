using FollowAlpha.LP.Domain.Positions;
using FluentAssertions;
using Xunit;

namespace FollowAlpha.LP.Domain.Tests.Positions;

public class IntentTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Intent_record_requires_a_reason()
    {
        var act = () => new IntentRecord(Intent.Harvest, T0, "   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void New_history_has_one_record_and_is_not_reclassified()
    {
        var history = new IntentHistory(new IntentRecord(Intent.Harvest, T0, "two-sided around price"));

        history.IsReclassified.Should().BeFalse();
        history.Original.Intent.Should().Be(Intent.Harvest);
        history.Current.Intent.Should().Be(Intent.Harvest);
        history.Records.Should().HaveCount(1);
    }

    [Fact]
    public void Reclassify_appends_and_preserves_the_original_immutably()
    {
        var original = new IntentHistory(new IntentRecord(Intent.Harvest, T0, "opened as harvest"));

        var reclassified = original.Reclassify(new IntentRecord(Intent.Accumulate, T1, "price fell; now accumulating"));

        // Original instance is unchanged (append-only, immutable).
        original.IsReclassified.Should().BeFalse();
        original.Records.Should().HaveCount(1);

        reclassified.IsReclassified.Should().BeTrue();
        reclassified.Original.Intent.Should().Be(Intent.Harvest);
        reclassified.Current.Intent.Should().Be(Intent.Accumulate);
        reclassified.Records.Should().HaveCount(2);
    }

    [Fact]
    public void Distinct_intents_are_first_seen_and_deduplicated()
    {
        var history = new IntentHistory(new IntentRecord(Intent.Harvest, T0, "a"))
            .Reclassify(new IntentRecord(Intent.Accumulate, T1, "b"))
            .Reclassify(new IntentRecord(Intent.Harvest, T1, "c"));

        history.DistinctIntents().Should().Equal(Intent.Harvest, Intent.Accumulate);
    }

    [Theory]
    [InlineData(Intent.Harvest, new[] { BenchmarkKind.Hodl, BenchmarkKind.FiftyFifty })]
    [InlineData(Intent.Accumulate, new[] { BenchmarkKind.LimitOrder })]
    [InlineData(Intent.Distribute, new[] { BenchmarkKind.LimitOrder })]
    public void Intent_maps_to_its_benchmarks(Intent intent, BenchmarkKind[] expected)
    {
        IntentBenchmarks.For(intent).Should().Equal(expected);
    }

    [Fact]
    public void Reclassified_history_uses_both_intents_benchmarks()
    {
        var history = new IntentHistory(new IntentRecord(Intent.Harvest, T0, "opened harvest"))
            .Reclassify(new IntentRecord(Intent.Accumulate, T1, "now accumulate"));

        IntentBenchmarks.For(history)
            .Should().Equal(BenchmarkKind.Hodl, BenchmarkKind.FiftyFifty, BenchmarkKind.LimitOrder);
    }

    [Fact]
    public void Single_intent_history_uses_only_that_intents_benchmarks()
    {
        var history = new IntentHistory(new IntentRecord(Intent.Harvest, T0, "harvest"));

        IntentBenchmarks.For(history).Should().Equal(BenchmarkKind.Hodl, BenchmarkKind.FiftyFifty);
    }
}
