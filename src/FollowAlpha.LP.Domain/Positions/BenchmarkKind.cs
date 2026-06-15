namespace FollowAlpha.LP.Domain.Positions;

/// <summary>The honest benchmarks a position's P&amp;L is measured against (LP-KNOWLEDGE.md §3).</summary>
public enum BenchmarkKind
{
    /// <summary>Hold the position's initial tokens.</summary>
    Hodl,

    /// <summary>Hold 50/50 by value at entry (no rebalance).</summary>
    FiftyFifty,

    /// <summary>A dry (no-fee) scaled limit order over the same range with the same capital.</summary>
    LimitOrder,
}
