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
        // Copy into an owned array: the official event/PnL series must not be adulterable after the
        // fact by mutating the list the simulator passed in.
        _events = [.. events];
        TotalRealizedPnl = totalRealizedPnl;
        TotalFees = totalFees;
        CompletedCrossings = completedCrossings;
        Breakouts = breakouts;
        Halted = halted;
        UnrealizedPnlAtEnd = unrealizedPnlAtEnd;
    }

    private readonly ChannelEvent[] _events;

    /// <summary>
    /// Every event in chronological order. Returned as a read-only wrapper over an owned array — it
    /// cannot be cast back to a mutable collection and used to tamper with the official series.
    /// </summary>
    public IReadOnlyList<ChannelEvent> Events => Array.AsReadOnly(_events);

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
