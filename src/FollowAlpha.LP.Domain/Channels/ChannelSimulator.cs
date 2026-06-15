using FollowAlpha.LP.Domain.Kernel;
using FollowAlpha.LP.Domain.Primitives;

namespace FollowAlpha.LP.Domain.Channels;

/// <summary>
/// Simulates the channel strategy over a price/fee series, producing the full event/PnL series including
/// breakouts (LP-KNOWLEDGE.md §5 Module 3; ARCHITECTURE.md §4.4). Each cycle is a single-sided LP that
/// buys the base asset at the channel bottom and sells it across the band as the price rises, valued with
/// the concentrated-liquidity kernel (so the channel uses the AMM curve, §4.2). The breakout protocol is
/// enforced purely from price levels and counters — never from the running PnL (LP-KNOWLEDGE.md §6.6).
///
/// <para><b>Declared semantics (flagged for analyst review).</b> A cycle opens when the price is at/below
/// the base (and at/above the no-reopen floor), deploying <c>capitalCapFraction · totalCapital</c> of
/// quote. It closes at the top (price ≥ upper) as a full crossing — realizing the geometric-mean sell —
/// which resets the reopen counter; or it breaks down (price &lt; lower) and is marked to market and
/// closed at a loss. After at most <c>maxReopensWithoutFullCrossing</c> opens without a crossing, or once
/// the price is below the no-reopen floor, the channel halts. A position still open at series end is
/// reported as unrealized, not realized.</para>
/// </summary>
public static class ChannelSimulator
{
    /// <summary>Runs the simulation.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="prices"/> or <paramref name="feesPerStep"/> is null.</exception>
    /// <exception cref="ArgumentException">The series differ in length or are empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A non-positive price, a negative fee, or non-positive total capital.</exception>
    public static ChannelSimulation Run(
        ChannelPolicy policy,
        IReadOnlyList<decimal> prices,
        IReadOnlyList<decimal> feesPerStep,
        decimal totalCapital)
    {
        ArgumentNullException.ThrowIfNull(prices);
        ArgumentNullException.ThrowIfNull(feesPerStep);
        if (prices.Count == 0)
        {
            throw new ArgumentException("Price series must be non-empty.", nameof(prices));
        }

        if (prices.Count != feesPerStep.Count)
        {
            throw new ArgumentException("Price and fee series must have the same length.", nameof(feesPerStep));
        }

        if (totalCapital <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCapital), totalCapital, "Total capital must be strictly positive.");
        }

        var sqrtLower = PriceMath.Sqrt(policy.LowerPrice);
        var sqrtUpper = PriceMath.Sqrt(policy.UpperPrice);
        var perCycleQuote = policy.CapitalCapFraction * totalCapital;

        var events = new List<ChannelEvent>();
        var open = false;
        var halted = false;
        var opensSinceCrossing = 0;
        var cumulativePnl = 0m;
        var totalFees = 0m;
        var completedCrossings = 0;
        var breakouts = 0;

        var liquidity = 0m;
        var cycleFees = 0m;

        for (var i = 0; i < prices.Count; i++)
        {
            var price = prices[i];
            if (price <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(prices), "Prices must be strictly positive.");
            }

            if (feesPerStep[i] < 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(feesPerStep), "Fees cannot be negative.");
            }

            if (open)
            {
                cycleFees += feesPerStep[i];

                if (price >= policy.UpperPrice)
                {
                    var pnl = ValueAt(liquidity, price, sqrtLower, sqrtUpper) + cycleFees - perCycleQuote;
                    cumulativePnl += pnl;
                    totalFees += cycleFees;
                    completedCrossings++;
                    opensSinceCrossing = 0;
                    open = false;
                    events.Add(new ChannelEvent(i, price, ChannelEventType.CloseAtTop, pnl, cumulativePnl, cycleFees));
                }
                else if (price < policy.LowerPrice)
                {
                    var pnl = ValueAt(liquidity, price, sqrtLower, sqrtUpper) + cycleFees - perCycleQuote;
                    cumulativePnl += pnl;
                    totalFees += cycleFees;
                    breakouts++;
                    open = false;
                    events.Add(new ChannelEvent(i, price, ChannelEventType.BreakoutDown, pnl, cumulativePnl, cycleFees));
                }
            }
            else if (!halted && price <= policy.LowerPrice)
            {
                if (price < policy.NoReopenFloorPrice)
                {
                    halted = true;
                    events.Add(new ChannelEvent(i, price, ChannelEventType.HaltedBelowFloor, 0m, cumulativePnl, 0m));
                }
                else if (opensSinceCrossing > policy.MaxReopensWithoutFullCrossing)
                {
                    halted = true;
                    events.Add(new ChannelEvent(i, price, ChannelEventType.HaltedMaxReopens, 0m, cumulativePnl, 0m));
                }
                else
                {
                    var token0 = perCycleQuote / price;
                    liquidity = token0 * sqrtLower * sqrtUpper / (sqrtUpper - sqrtLower);
                    cycleFees = 0m;
                    open = true;
                    opensSinceCrossing++;
                    events.Add(new ChannelEvent(i, price, ChannelEventType.Open, 0m, cumulativePnl, 0m));
                }
            }
        }

        var unrealized = open
            ? ValueAt(liquidity, prices[^1], sqrtLower, sqrtUpper) + cycleFees - perCycleQuote
            : 0m;

        return new ChannelSimulation(events, cumulativePnl, totalFees, completedCrossings, breakouts, halted, unrealized);
    }

    private static decimal ValueAt(decimal liquidity, decimal price, decimal sqrtLower, decimal sqrtUpper)
    {
        var sqrtPrice = PriceMath.Sqrt(price);
        var token0 = LiquidityMath.CalculateX(liquidity, sqrtPrice, sqrtLower, sqrtUpper);
        var token1 = LiquidityMath.CalculateY(liquidity, sqrtPrice, sqrtLower, sqrtUpper);
        return token1 + token0 * price;
    }
}
