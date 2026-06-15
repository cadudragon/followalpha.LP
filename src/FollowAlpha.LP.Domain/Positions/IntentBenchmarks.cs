namespace FollowAlpha.LP.Domain.Positions;

/// <summary>
/// The benchmark mapping (LP-KNOWLEDGE.md §3): which honest benchmarks a position's P&amp;L is judged
/// against. For a reclassified position the set is the union over all distinct intents it has carried —
/// the dual-benchmark rule of ARCHITECTURE.md §4.3.
/// </summary>
public static class IntentBenchmarks
{
    /// <summary>The benchmarks for a single intent.</summary>
    public static IReadOnlyList<BenchmarkKind> For(Intent intent) => intent switch
    {
        Intent.Harvest => [BenchmarkKind.Hodl, BenchmarkKind.FiftyFifty],
        Intent.Accumulate => [BenchmarkKind.LimitOrder],
        Intent.Distribute => [BenchmarkKind.LimitOrder],
        _ => throw new ArgumentOutOfRangeException(nameof(intent), intent, "Unknown intent."),
    };

    /// <summary>
    /// The benchmarks for a position's full intent history (deduplicated, first-seen order). Equals the
    /// single-intent set when never reclassified; the union of both intents' benchmarks otherwise.
    /// </summary>
    public static IReadOnlyList<BenchmarkKind> For(IntentHistory history)
    {
        ArgumentNullException.ThrowIfNull(history);

        var kinds = new List<BenchmarkKind>();
        foreach (var intent in history.DistinctIntents())
        {
            foreach (var kind in For(intent))
            {
                if (!kinds.Contains(kind))
                {
                    kinds.Add(kind);
                }
            }
        }

        return kinds;
    }
}
