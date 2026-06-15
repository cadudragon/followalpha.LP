using System.Globalization;

namespace FollowAlpha.LP.Domain.Positions;

/// <summary>
/// The full identity of a benchmark a position is judged against. For <see cref="BenchmarkKind.Hodl"/>
/// and <see cref="BenchmarkKind.FiftyFifty"/> the kind alone is enough; for
/// <see cref="BenchmarkKind.LimitOrder"/> the identity also includes the <see cref="LadderSide"/>
/// (accumulate buys token0, distribute sells token0) and the <see cref="LimitLadder"/> shape
/// (primary <c>UniformQuoteByPrice</c> vs secondary <c>UniformBaseByPrice</c>). This is what lets a
/// reclassified position keep both perspectives distinct instead of collapsing them to one
/// "LimitOrder" (ARCHITECTURE.md §4.3).
/// </summary>
public readonly record struct BenchmarkSpec
{
    private BenchmarkSpec(BenchmarkKind kind, LadderSide? side, LimitLadder? ladder)
    {
        Kind = kind;
        Side = side;
        Ladder = ladder;
    }

    /// <summary>The benchmark category.</summary>
    public BenchmarkKind Kind { get; }

    /// <summary>The ladder direction — set only when <see cref="Kind"/> is <see cref="BenchmarkKind.LimitOrder"/>.</summary>
    public LadderSide? Side { get; }

    /// <summary>The ladder shape — set only when <see cref="Kind"/> is <see cref="BenchmarkKind.LimitOrder"/>.</summary>
    public LimitLadder? Ladder { get; }

    /// <summary>Hold the position's initial tokens.</summary>
    public static BenchmarkSpec Hodl { get; } = new(BenchmarkKind.Hodl, null, null);

    /// <summary>Hold 50/50 by value at entry.</summary>
    public static BenchmarkSpec FiftyFifty { get; } = new(BenchmarkKind.FiftyFifty, null, null);

    /// <summary>A dry scaled limit order with the given direction and ladder shape.</summary>
    public static BenchmarkSpec LimitOrder(LadderSide side, LimitLadder ladder) =>
        new(BenchmarkKind.LimitOrder, side, ladder);

    /// <summary>A stable, human-readable identity (e.g. <c>LimitOrder/Accumulate/UniformQuoteByPrice</c>).</summary>
    public override string ToString() => Kind == BenchmarkKind.LimitOrder
        ? string.Create(CultureInfo.InvariantCulture, $"{Kind}/{Side}/{Ladder}")
        : Kind.ToString();
}
