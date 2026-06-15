namespace FollowAlpha.LP.Domain.Positions;

/// <summary>
/// The declared intent of an LP position — the lens its P&amp;L is judged through (LP-KNOWLEDGE.md §3).
/// Impermanent loss is only a loss against a benchmark you did not want; the intent fixes that benchmark.
/// </summary>
public enum Intent
{
    /// <summary>Single-sided range below price: a paid scaled limit buy. Benchmark: <see cref="BenchmarkKind.LimitOrder"/>.</summary>
    Accumulate,

    /// <summary>Single-sided range above price: a paid scaled limit sell. Benchmark: <see cref="BenchmarkKind.LimitOrder"/>.</summary>
    Distribute,

    /// <summary>Two-sided range around price. Benchmarks: <see cref="BenchmarkKind.Hodl"/> and <see cref="BenchmarkKind.FiftyFifty"/>.</summary>
    Harvest,
}
