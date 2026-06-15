namespace FollowAlpha.LP.Domain.Signals;

/// <summary>
/// Empirical band-survival (LP-KNOWLEDGE.md §2: nobody predicts permanence; you measure the historical
/// time-to-exit distribution of a band of width W). Definition (declared before results, §6.1): for each
/// entry index a <b>relative</b> band <c>[p·(1−W), p·(1+W)]</c> is centred on that entry's price; the
/// number of steps until the price first leaves the band (strictly outside, inclusive bounds) is
/// recorded. Windows overlap (every entry is a start); entries that never exit before the series ends
/// are right-censored. Pure given the series.
/// </summary>
public static class BandSurvivalEstimator
{
    /// <summary>Builds the time-to-exit distribution for a relative band of half-width <paramref name="widthFraction"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="prices"/> is null.</exception>
    /// <exception cref="ArgumentException">Fewer than 2 prices.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Width is not in (0, 1), or a price is not strictly positive.</exception>
    public static BandSurvival ForWidth(IReadOnlyList<decimal> prices, decimal widthFraction)
    {
        ArgumentNullException.ThrowIfNull(prices);
        if (prices.Count < 2)
        {
            throw new ArgumentException("At least 2 prices are required.", nameof(prices));
        }

        if (widthFraction <= 0m || widthFraction >= 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(widthFraction), widthFraction, "Width must be in (0, 1).");
        }

        var exits = new List<int>();
        var censored = 0;

        for (var start = 0; start < prices.Count - 1; start++)
        {
            var anchor = prices[start];
            if (anchor <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(prices), "Prices must be strictly positive.");
            }

            var lower = anchor * (1m - widthFraction);
            var upper = anchor * (1m + widthFraction);

            var exited = false;
            for (var j = start + 1; j < prices.Count; j++)
            {
                if (prices[j] < lower || prices[j] > upper)
                {
                    exits.Add(j - start);
                    exited = true;
                    break;
                }
            }

            if (!exited)
            {
                censored++;
            }
        }

        exits.Sort();
        return new BandSurvival([.. exits], censored);
    }
}
