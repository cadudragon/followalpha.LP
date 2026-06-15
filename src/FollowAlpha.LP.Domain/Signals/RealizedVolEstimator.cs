using FollowAlpha.LP.Domain.Primitives;

namespace FollowAlpha.LP.Domain.Signals;

/// <summary>
/// Realized volatility from a price series (pure given the series). Definition (declared before any
/// results, per LP-KNOWLEDGE.md §6.1): the <b>sample</b> standard deviation (÷ n−1) of close-to-close
/// <b>log returns</b>, annualized by <c>·sqrt(periodsPerYear)</c>. Deterministic decimal.
/// </summary>
public static class RealizedVolEstimator
{
    /// <summary>The per-period standard deviation of log returns (not annualized).</summary>
    /// <exception cref="ArgumentNullException"><paramref name="prices"/> is null.</exception>
    /// <exception cref="ArgumentException">Fewer than 3 prices (need ≥ 2 returns for a sample stddev).</exception>
    /// <exception cref="ArgumentOutOfRangeException">A price is not strictly positive.</exception>
    public static decimal StdDevOfLogReturns(IReadOnlyList<decimal> prices)
    {
        ArgumentNullException.ThrowIfNull(prices);
        if (prices.Count < 3)
        {
            throw new ArgumentException("At least 3 prices are required (two log returns).", nameof(prices));
        }

        var returns = new decimal[prices.Count - 1];
        var sum = 0m;
        for (var i = 1; i < prices.Count; i++)
        {
            if (prices[i] <= 0m || prices[i - 1] <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(prices), "Prices must be strictly positive.");
            }

            var r = PriceMath.Ln(prices[i] / prices[i - 1]);
            returns[i - 1] = r;
            sum += r;
        }

        var mean = sum / returns.Length;
        var sumSquares = 0m;
        foreach (var r in returns)
        {
            var d = r - mean;
            sumSquares += d * d;
        }

        var variance = sumSquares / (returns.Length - 1);
        return PriceMath.Sqrt(variance);
    }

    /// <summary>Annualized realized volatility: per-period stddev × sqrt(periodsPerYear).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="periodsPerYear"/> is not strictly positive.</exception>
    public static decimal Annualized(IReadOnlyList<decimal> prices, int periodsPerYear)
    {
        if (periodsPerYear <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(periodsPerYear), periodsPerYear, "Periods per year must be strictly positive.");
        }

        return StdDevOfLogReturns(prices) * PriceMath.Sqrt(periodsPerYear);
    }
}
