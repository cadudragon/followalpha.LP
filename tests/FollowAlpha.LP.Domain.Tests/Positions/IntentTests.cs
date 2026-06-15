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

    // Benchmark specs, by full identity (kind + side + ladder for limit orders).
    private static readonly BenchmarkSpec AccQuote = BenchmarkSpec.LimitOrder(LadderSide.Accumulate, LimitLadder.UniformQuoteByPrice);
    private static readonly BenchmarkSpec AccBase = BenchmarkSpec.LimitOrder(LadderSide.Accumulate, LimitLadder.UniformBaseByPrice);
    private static readonly BenchmarkSpec DistQuote = BenchmarkSpec.LimitOrder(LadderSide.Distribute, LimitLadder.UniformQuoteByPrice);
    private static readonly BenchmarkSpec DistBase = BenchmarkSpec.LimitOrder(LadderSide.Distribute, LimitLadder.UniformBaseByPrice);

    [Fact]
    public void Harvest_maps_to_hodl_and_fifty_fifty()
    {
        IntentBenchmarks.For(Intent.Harvest).Should().Equal(BenchmarkSpec.Hodl, BenchmarkSpec.FiftyFifty);
    }

    [Fact]
    public void Accumulate_maps_to_both_accumulate_limit_specs()
    {
        IntentBenchmarks.For(Intent.Accumulate).Should().Equal(AccQuote, AccBase);
    }

    [Fact]
    public void Distribute_maps_to_both_distribute_limit_specs()
    {
        IntentBenchmarks.For(Intent.Distribute).Should().Equal(DistQuote, DistBase);
    }

    [Fact]
    public void Accumulate_and_distribute_specs_are_distinct_identities()
    {
        // The crux: accumulate and distribute limit orders must not collapse to one "LimitOrder".
        AccQuote.Should().NotBe(DistQuote);
        IntentBenchmarks.For(Intent.Accumulate).Should().NotContain(DistQuote);
    }

    [Fact]
    public void Reclassify_accumulate_to_distribute_yields_four_distinct_specs()
    {
        var history = new IntentHistory(new IntentRecord(Intent.Accumulate, T0, "accumulate below"))
            .Reclassify(new IntentRecord(Intent.Distribute, T1, "flipped to distribute above"));

        IntentBenchmarks.For(history).Should().Equal(AccQuote, AccBase, DistQuote, DistBase);
    }

    [Fact]
    public void Reclassify_harvest_to_accumulate_yields_hodl_fifty_and_both_accumulate()
    {
        var history = new IntentHistory(new IntentRecord(Intent.Harvest, T0, "opened harvest"))
            .Reclassify(new IntentRecord(Intent.Accumulate, T1, "now accumulate"));

        IntentBenchmarks.For(history)
            .Should().Equal(BenchmarkSpec.Hodl, BenchmarkSpec.FiftyFifty, AccQuote, AccBase);
    }

    [Fact]
    public void Single_intent_history_uses_only_that_intents_specs()
    {
        var history = new IntentHistory(new IntentRecord(Intent.Harvest, T0, "harvest"));

        IntentBenchmarks.For(history).Should().Equal(BenchmarkSpec.Hodl, BenchmarkSpec.FiftyFifty);
    }

    [Fact]
    public void Repeated_reclassification_to_a_seen_intent_does_not_duplicate_specs()
    {
        var history = new IntentHistory(new IntentRecord(Intent.Harvest, T0, "harvest"))
            .Reclassify(new IntentRecord(Intent.Accumulate, T1, "accumulate"))
            .Reclassify(new IntentRecord(Intent.Harvest, T1, "back to harvest"));

        IntentBenchmarks.For(history)
            .Should().Equal(BenchmarkSpec.Hodl, BenchmarkSpec.FiftyFifty, AccQuote, AccBase);
    }
}
