namespace FollowAlpha.LP.Domain.Signals;

/// <summary>The fee-share estimate: your share of in-range fees, expected daily fees, and the while-in-range fee APR.</summary>
public readonly record struct FeeShareEstimate(decimal FeeShare, decimal ExpectedDailyFees, decimal FeeApr);

/// <summary>
/// Expected fee capture for a position (ARCHITECTURE.md §4.4). Your share of fees is your liquidity over
/// the total liquidity active in the band (<c>ownL / inRangeL</c>, which includes your own — adding
/// liquidity dilutes the share). The reported APR is the <b>while-in-range</b> rate (assumes the price
/// stays in the band); the time adjustment for the band's survival is the caller's job
/// (<see cref="BandSurvivalEstimator"/>). Pure decimal.
/// </summary>
public static class FeeShareEstimator
{
    /// <summary>
    /// Estimates fee share and while-in-range fee APR from the fee fraction, recent daily volume, own and
    /// in-range liquidity, and the position's deployed capital (token1 numeraire).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A negative fee/volume/liquidity, non-positive in-range liquidity or capital, or own &gt; in-range.</exception>
    public static FeeShareEstimate Estimate(
        decimal feeFraction,
        decimal dailyVolume,
        decimal ownLiquidity,
        decimal inRangeLiquidity,
        decimal positionValue)
    {
        if (feeFraction < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(feeFraction), feeFraction, "Fee fraction cannot be negative.");
        }

        if (dailyVolume < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(dailyVolume), dailyVolume, "Daily volume cannot be negative.");
        }

        if (ownLiquidity < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(ownLiquidity), ownLiquidity, "Own liquidity cannot be negative.");
        }

        if (inRangeLiquidity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(inRangeLiquidity), inRangeLiquidity, "In-range liquidity must be strictly positive.");
        }

        if (ownLiquidity > inRangeLiquidity)
        {
            throw new ArgumentOutOfRangeException(nameof(ownLiquidity), ownLiquidity, "Own liquidity cannot exceed in-range liquidity (it is included in it).");
        }

        if (positionValue <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(positionValue), positionValue, "Position value must be strictly positive.");
        }

        var share = ownLiquidity / inRangeLiquidity;
        var dailyFees = feeFraction * dailyVolume * share;
        var apr = dailyFees * 365m / positionValue;
        return new FeeShareEstimate(share, dailyFees, apr);
    }
}
