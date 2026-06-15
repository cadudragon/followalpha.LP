namespace FollowAlpha.LP.Domain.Positions;

/// <summary>
/// The benchmark mapping (LP-KNOWLEDGE.md §3): which honest benchmarks a position's P&amp;L is judged
/// against, as full <see cref="BenchmarkSpec"/> identities (kind + side + ladder for limit orders). For
/// a reclassified position the set is the union over all distinct intents it has carried, deduplicated
/// by full spec in first-seen order — the dual-benchmark rule of ARCHITECTURE.md §4.3.
/// </summary>
public static class IntentBenchmarks
{
    /// <summary>The benchmark specs for a single intent (primary ladder before secondary for limit orders).</summary>
    public static IReadOnlyList<BenchmarkSpec> For(Intent intent) => intent switch
    {
        Intent.Harvest =>
        [
            BenchmarkSpec.Hodl,
            BenchmarkSpec.FiftyFifty,
        ],
        Intent.Accumulate =>
        [
            BenchmarkSpec.LimitOrder(LadderSide.Accumulate, LimitLadder.UniformQuoteByPrice),
            BenchmarkSpec.LimitOrder(LadderSide.Accumulate, LimitLadder.UniformBaseByPrice),
        ],
        Intent.Distribute =>
        [
            BenchmarkSpec.LimitOrder(LadderSide.Distribute, LimitLadder.UniformQuoteByPrice),
            BenchmarkSpec.LimitOrder(LadderSide.Distribute, LimitLadder.UniformBaseByPrice),
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(intent), intent, "Unknown intent."),
    };

    /// <summary>
    /// The benchmark specs for a position's full intent history, deduplicated by full spec in first-seen
    /// order. Equals the single-intent set when never reclassified; the union of every intent's specs
    /// otherwise (e.g. Accumulate→Distribute yields four distinct limit-order specs, not one).
    /// </summary>
    public static IReadOnlyList<BenchmarkSpec> For(IntentHistory history)
    {
        ArgumentNullException.ThrowIfNull(history);

        var specs = new List<BenchmarkSpec>();
        foreach (var intent in history.DistinctIntents())
        {
            foreach (var spec in For(intent))
            {
                if (!specs.Contains(spec))
                {
                    specs.Add(spec);
                }
            }
        }

        return specs;
    }
}
