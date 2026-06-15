namespace FollowAlpha.LP.Domain.Signals;

/// <summary>
/// Trendiness as Kaufman's <b>efficiency ratio</b> (the path-efficiency form of ARCHITECTURE.md §4.4):
/// net displacement divided by total path length over the series. 1 = a perfectly straight trend, near
/// 0 = choppy/round-tripping (range-like). Direction is never emitted — only how trending vs ranging
/// (RN-07; LP-KNOWLEDGE.md §2: vol/regime is tractable, direction is not). Pure decimal.
/// </summary>
public static class TrendinessEstimator
{
    /// <summary>The efficiency ratio of the price series, in [0, 1].</summary>
    /// <exception cref="ArgumentNullException"><paramref name="prices"/> is null.</exception>
    /// <exception cref="ArgumentException">Fewer than 2 prices.</exception>
    public static decimal PathEfficiency(IReadOnlyList<decimal> prices)
    {
        ArgumentNullException.ThrowIfNull(prices);
        if (prices.Count < 2)
        {
            throw new ArgumentException("At least 2 prices are required.", nameof(prices));
        }

        var netMovement = Math.Abs(prices[^1] - prices[0]);
        var pathLength = 0m;
        for (var i = 1; i < prices.Count; i++)
        {
            pathLength += Math.Abs(prices[i] - prices[i - 1]);
        }

        return pathLength == 0m ? 0m : netMovement / pathLength;
    }
}
