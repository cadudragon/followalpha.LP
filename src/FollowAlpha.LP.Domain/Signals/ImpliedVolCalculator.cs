using FollowAlpha.LP.Domain.Primitives;

namespace FollowAlpha.LP.Domain.Signals;

/// <summary>
/// Pool-implied volatility (LP-KNOWLEDGE.md §6b — "the jewel"): how much volatility the pool is paying
/// for. <c>IV = 2·fee·sqrt(dailyVolume / tickTvl)·sqrt(365)</c>, returned as an annualized fraction
/// (multiply by 100 for a percent). The verdict lens: selling vol is attractive when the pool's IV is
/// above forecast realized vol. Pure decimal (deterministic).
/// </summary>
public static class ImpliedVolCalculator
{
    private static readonly decimal Sqrt365 = PriceMath.Sqrt(365m);

    /// <summary>
    /// Computes implied volatility from the fee fraction (e.g. 0.003 for the 0.3% tier — see
    /// <see cref="FeeTier.FeeFraction"/>), the recent daily volume, and the TVL active in the tick band.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Fee or volume is negative, or tick TVL is not strictly positive.</exception>
    public static decimal Calculate(decimal feeFraction, decimal dailyVolume, decimal tickTvl)
    {
        if (feeFraction < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(feeFraction), feeFraction, "Fee fraction cannot be negative.");
        }

        if (dailyVolume < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(dailyVolume), dailyVolume, "Daily volume cannot be negative.");
        }

        if (tickTvl <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(tickTvl), tickTvl, "Tick TVL must be strictly positive.");
        }

        return 2m * feeFraction * PriceMath.Sqrt(dailyVolume / tickTvl) * Sqrt365;
    }
}
