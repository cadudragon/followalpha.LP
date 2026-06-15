namespace FollowAlpha.LP.Domain.Channels;

/// <summary>
/// The channel strategy's mandatory safety manual, declared before the first open (LP-KNOWLEDGE.md §5
/// Module 3): the channel band, the capital cap, and the breakout protocol (max reopens without a full
/// crossing, and the no-reopen floor). All decisions in <see cref="ChannelSimulator"/> are a function of
/// these price levels and counters — never of the running PnL (LP-KNOWLEDGE.md §6.6).
/// </summary>
public readonly record struct ChannelPolicy
{
    /// <summary>Constructs a channel policy.</summary>
    /// <exception cref="ArgumentException">The band is not <c>0 &lt; lower &lt; upper</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Capital-cap fraction not in (0,1], max reopens negative, or floor not strictly positive.</exception>
    public ChannelPolicy(
        decimal lowerPrice,
        decimal upperPrice,
        decimal capitalCapFraction,
        int maxReopensWithoutFullCrossing,
        decimal noReopenFloorPrice)
    {
        if (lowerPrice <= 0m || lowerPrice >= upperPrice)
        {
            throw new ArgumentException("Channel must satisfy 0 < lowerPrice < upperPrice.", nameof(lowerPrice));
        }

        if (capitalCapFraction <= 0m || capitalCapFraction > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(capitalCapFraction), capitalCapFraction, "Capital-cap fraction must be in (0, 1].");
        }

        if (maxReopensWithoutFullCrossing < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxReopensWithoutFullCrossing), maxReopensWithoutFullCrossing, "Max reopens cannot be negative.");
        }

        if (noReopenFloorPrice <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(noReopenFloorPrice), noReopenFloorPrice, "No-reopen floor must be strictly positive.");
        }

        LowerPrice = lowerPrice;
        UpperPrice = upperPrice;
        CapitalCapFraction = capitalCapFraction;
        MaxReopensWithoutFullCrossing = maxReopensWithoutFullCrossing;
        NoReopenFloorPrice = noReopenFloorPrice;
    }

    /// <summary>Bottom of the channel (a) — where a cycle opens.</summary>
    public decimal LowerPrice { get; }

    /// <summary>Top of the channel (b) — where a cycle closes (fully converted to quote).</summary>
    public decimal UpperPrice { get; }

    /// <summary>Capital deployed per cycle as a fraction of total LP capital (the per-channel cap).</summary>
    public decimal CapitalCapFraction { get; }

    /// <summary>Maximum reopens allowed without a completed bottom→top crossing before the channel halts.</summary>
    public int MaxReopensWithoutFullCrossing { get; }

    /// <summary>Price below which the channel never (re)opens — accept the inventory / stop, do not catch the knife.</summary>
    public decimal NoReopenFloorPrice { get; }
}
