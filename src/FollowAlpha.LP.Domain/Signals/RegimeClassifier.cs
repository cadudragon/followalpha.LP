namespace FollowAlpha.LP.Domain.Signals;

/// <summary>
/// Classifies an asset's volatility regime (<see cref="Regime"/>) from a close-price series, composing the
/// Phase-1 estimators: the percentile of the current short-window realized vol within a lookback of rolling
/// RV samples (is vol elevated for this asset?), and trendiness as path-efficiency (is the path directional
/// or choppy?). Pure and deterministic; <b>never emits direction</b> (RN-07) and the thresholds are declared
/// in <see cref="RegimePolicy"/>, never tuned against outcomes (RN-14). The Application layer checks
/// <see cref="HasEnoughData"/> first and raises the 422 path when history is thin — <see cref="Classify"/>
/// also guards as a safety net.
/// </summary>
public static class RegimeClassifier
{
    /// <summary>Whether there are enough bars to classify at all (else the caller returns insufficient-data).</summary>
    public static bool HasEnoughData(int sampleCount, RegimePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return sampleCount >= policy.MinBars;
    }

    /// <param name="prices">Close prices, oldest first.</param>
    /// <exception cref="ArgumentException">Fewer than <see cref="RegimePolicy.MinBars"/> prices.</exception>
    public static RegimeResult Classify(IReadOnlyList<decimal> prices, RegimePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(prices);
        ArgumentNullException.ThrowIfNull(policy);
        if (!HasEnoughData(prices.Count, policy))
        {
            throw new ArgumentException($"At least {policy.MinBars} prices are required to classify a regime.", nameof(prices));
        }

        var trendiness = TrendinessEstimator.PathEfficiency(LastN(prices, policy.TrendinessWindow));
        var rvPercentile = CurrentRvPercentile(prices, policy);
        var (regime, reason) = Decide(trendiness, rvPercentile, policy);

        return new RegimeResult(
            regime,
            new RegimeEvidence(
                RvPercentile: rvPercentile,
                Trendiness: trendiness,
                RvWindow: policy.RvWindow,
                PercentileLookback: policy.PercentileLookback,
                TrendinessWindow: policy.TrendinessWindow,
                MinBars: policy.MinBars,
                SampleCount: prices.Count,
                ClassificationReason: reason));
    }

    // The percentile (0-100) of the most recent RV window within the distribution of rolling RV windows
    // over the lookback. Higher = vol is elevated relative to this asset's own recent history.
    private static decimal CurrentRvPercentile(IReadOnlyList<decimal> prices, RegimePolicy policy)
    {
        var lookback = LastN(prices, policy.PercentileLookback);
        var windowPrices = policy.RvWindow + 1; // RvWindow returns need RvWindow+1 prices
        var samples = new List<decimal>(lookback.Count);
        for (var end = windowPrices; end <= lookback.Count; end++)
        {
            samples.Add(RealizedVolEstimator.Annualized(Range(lookback, end - windowPrices, windowPrices), policy.PeriodsPerYear));
        }

        // Midrank percentile: a stationary series (all RV windows ~equal) reads ~50, not ~100, so "current
        // vol is elevated" means genuinely above this asset's own recent history — not merely tied with it.
        var current = samples[^1];
        var below = samples.Count(s => s < current);
        var equal = samples.Count(s => s == current);
        return 100m * (below + (0.5m * equal)) / samples.Count;
    }

    // Trendiness (directional vs choppy) is the primary axis — a directional path is the LP-hostile case
    // regardless of vol magnitude. Among choppy markets, the RV percentile separates a calm range from an
    // agitated, unstable regime. Declared rule, not tuned (RN-14).
    private static (Regime Regime, string Reason) Decide(decimal trendiness, decimal rvPercentile, RegimePolicy policy)
    {
        if (trendiness >= policy.TrendinessCutoff)
        {
            return (Regime.Trending, $"trendiness {trendiness:0.00} >= {policy.TrendinessCutoff:0.00} (directional path)");
        }

        if (rvPercentile <= policy.RangeRvPercentileMax)
        {
            return (Regime.Range, $"choppy (trendiness {trendiness:0.00} < {policy.TrendinessCutoff:0.00}) and RV percentile {rvPercentile:0.#} <= {policy.RangeRvPercentileMax:0.#}");
        }

        return (Regime.Transition, $"choppy (trendiness {trendiness:0.00} < {policy.TrendinessCutoff:0.00}) but RV percentile {rvPercentile:0.#} > {policy.RangeRvPercentileMax:0.#} (unstable)");
    }

    // The last n elements (or all when fewer), preserving order.
    private static IReadOnlyList<decimal> LastN(IReadOnlyList<decimal> prices, int n) =>
        prices.Count <= n ? prices : Range(prices, prices.Count - n, n);

    private static decimal[] Range(IReadOnlyList<decimal> prices, int start, int length)
    {
        var slice = new decimal[length];
        for (var i = 0; i < length; i++)
        {
            slice[i] = prices[start + i];
        }

        return slice;
    }
}
