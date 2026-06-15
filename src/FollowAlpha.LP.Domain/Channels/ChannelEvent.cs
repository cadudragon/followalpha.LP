namespace FollowAlpha.LP.Domain.Channels;

/// <summary>What happened at a step of the channel simulation.</summary>
public enum ChannelEventType
{
    /// <summary>A cycle opened at the base (capital deployed).</summary>
    Open,

    /// <summary>Price reached the top: the cycle closed fully converted to quote (a full crossing).</summary>
    CloseAtTop,

    /// <summary>Price broke below the channel: the cycle is marked to market and closed at a loss.</summary>
    BreakoutDown,

    /// <summary>The channel halted because price is below the no-reopen floor.</summary>
    HaltedBelowFloor,

    /// <summary>The channel halted because the max reopens without a full crossing was exceeded.</summary>
    HaltedMaxReopens,
}

/// <summary>
/// One event in the channel's full series (never cherry-picked — breakouts and halts are recorded too;
/// LP-KNOWLEDGE.md §5/§6.3). <see cref="RealizedPnl"/> is the cycle's realized PnL for close/breakout
/// events (token1 numeraire), 0 otherwise; <see cref="CumulativePnl"/> is the running realized total.
/// </summary>
public readonly record struct ChannelEvent(
    int StepIndex,
    decimal Price,
    ChannelEventType Type,
    decimal RealizedPnl,
    decimal CumulativePnl,
    decimal CycleFees);
