namespace FollowAlpha.LP.Domain.Channels;

/// <summary>
/// The full result of a channel simulation: every event in order, the realized totals, and any
/// open-position value left unrealized at the end of the series. This is the <b>official metric</b> —
/// the complete series including breakouts, not the good run (LP-KNOWLEDGE.md §5).
/// </summary>
public sealed record ChannelSimulation
{
    internal ChannelSimulation(
        IReadOnlyList<ChannelEvent> events,
        decimal totalRealizedPnl,
        decimal totalFees,
        int completedCrossings,
        int breakouts,
        bool halted,
        decimal unrealizedPnlAtEnd)
    {
        Events = events;
        TotalRealizedPnl = totalRealizedPnl;
        TotalFees = totalFees;
        CompletedCrossings = completedCrossings;
        Breakouts = breakouts;
        Halted = halted;
        UnrealizedPnlAtEnd = unrealizedPnlAtEnd;
    }

    /// <summary>Every event in chronological order.</summary>
    public IReadOnlyList<ChannelEvent> Events { get; }

    /// <summary>Sum of realized cycle PnL (token1 numeraire).</summary>
    public decimal TotalRealizedPnl { get; }

    /// <summary>Total fees accrued across all cycles.</summary>
    public decimal TotalFees { get; }

    /// <summary>Number of cycles that completed a bottom→top crossing.</summary>
    public int CompletedCrossings { get; }

    /// <summary>Number of cycles that broke down out of the channel.</summary>
    public int Breakouts { get; }

    /// <summary>True if the breakout protocol halted the channel (floor or max reopens).</summary>
    public bool Halted { get; }

    /// <summary>Mark-to-market PnL of a position still open at the end of the series (0 if flat).</summary>
    public decimal UnrealizedPnlAtEnd { get; }
}
