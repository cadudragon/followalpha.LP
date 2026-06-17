using System.Globalization;
using System.Numerics;
using FollowAlpha.LP.Application.Persistence;
using FollowAlpha.LP.Domain.Signals;

namespace FollowAlpha.LP.Application.Exploration;

/// <summary>
/// Shared derivations for the exploration use cases — realized-vol summary, pool IV (with its declared
/// basis), and competing liquidity (raw L figures over a tick band). Each reuses the Phase-1 Domain
/// estimators and emits values with their basis so nothing reads as more precise than it is.
/// </summary>
internal static class ExplorationMetrics
{
    public static string Money(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    public static RvSummary RvSummary(IReadOnlyList<decimal> closes, ExplorationPolicy policy) => new(
        Rv(closes, policy.RvSummaryShort, policy.PeriodsPerYear),
        Rv(closes, policy.RvSummaryMedium, policy.PeriodsPerYear),
        Rv(closes, policy.RvSummaryLong, policy.PeriodsPerYear));

    /// <summary>Annualized realized vol over the last <paramref name="window"/> returns, or null if history is too thin / non-positive.</summary>
    public static decimal? Rv(IReadOnlyList<decimal> closes, int window, int periodsPerYear)
    {
        if (closes.Count < window + 1)
        {
            return null;
        }

        var slice = new decimal[window + 1];
        for (var i = 0; i < slice.Length; i++)
        {
            var value = closes[closes.Count - slice.Length + i];
            if (value <= 0m)
            {
                return null; // bad bar — never throw into a 500 from a read endpoint
            }

            slice[i] = value;
        }

        return RealizedVolEstimator.Annualized(slice, periodsPerYear);
    }

    /// <summary>Pool IV with its inputs echoed and basis declared (`pool_tvl_total`); `annualized` null when TVL ≤ 0.</summary>
    public static PoolIvDto Iv(Pool pool, PoolSnapshot snapshot)
    {
        // Uniswap fee units are 1e-6 (== Domain FeeTier.FeeFraction); 500 -> 0.0005.
        var feeFraction = pool.FeeTier / 1_000_000m;
        decimal? annualized = snapshot.Tvl > 0m && snapshot.DayVolumeUsd >= 0m
            ? ImpliedVolCalculator.Calculate(feeFraction, snapshot.DayVolumeUsd, snapshot.Tvl)
            : null;

        return new PoolIvDto(annualized, ExplorationWire.IvBasisPoolTvlTotal, Money(snapshot.DayVolumeUsd), Money(snapshot.Tvl), snapshot.AsOfUtc);
    }

    /// <summary>Competing liquidity: the snapshot's active L, plus summed gross liquidity over the ±bandPct tick band.</summary>
    public static CompetingLiquidityDto Competing(
        Pool pool, PoolSnapshot snapshot, IReadOnlyList<TickLiquiditySnapshot> ticks, decimal bandPct)
    {
        var deltaTicks = (int)Math.Round(Math.Log(1.0 + ((double)bandPct / 100.0)) / Math.Log(1.0001));
        var lower = SnapDown(snapshot.CurrentTick - deltaTicks, pool.TickSpacing);
        var upper = SnapUp(snapshot.CurrentTick + deltaTicks, pool.TickSpacing);

        var density = BigInteger.Zero;
        foreach (var t in ticks)
        {
            if (t.Tick >= lower && t.Tick <= upper)
            {
                density += BigInteger.Parse(t.LiquidityGross, CultureInfo.InvariantCulture);
            }
        }

        return new CompetingLiquidityDto(
            snapshot.Liquidity, density.ToString(CultureInfo.InvariantCulture), bandPct, lower, upper, snapshot.AsOfUtc);
    }

    /// <summary>An empty competing-liquidity object (raw figures null) for stale/absent snapshots — keeps the band declared.</summary>
    public static CompetingLiquidityDto NoCompeting(decimal bandPct, DateTimeOffset? asOfUtc) =>
        new(null, null, bandPct, null, null, asOfUtc);

    public static PoolIvDto NoIv(DateTimeOffset? asOfUtc) =>
        new(null, ExplorationWire.IvBasisPoolTvlTotal, null, null, asOfUtc);

    public static string Pair(Pool pool, IReadOnlyDictionary<string, string> symbols) =>
        $"{symbols.GetValueOrDefault(pool.Token0AssetId, pool.Token0AssetId)}/{symbols.GetValueOrDefault(pool.Token1AssetId, pool.Token1AssetId)}";

    private static int SnapDown(int tick, int spacing) => (int)Math.Floor(tick / (double)spacing) * spacing;

    private static int SnapUp(int tick, int spacing) => (int)Math.Ceiling(tick / (double)spacing) * spacing;
}
