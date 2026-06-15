namespace FollowAlpha.LP.Domain.Signals;

/// <summary>
/// The empirical time-to-exit distribution of a band over a price series: how many steps each entry
/// stayed inside the band before leaving. <see cref="CensoredCount"/> counts entries that reached the
/// end of the series without exiting (right-censored) — reported, not modelled away. Quantiles are over
/// the observed exits only (a survival-curve / Kaplan–Meier treatment is a later refinement).
/// </summary>
public readonly record struct BandSurvival
{
    private readonly int[] _sortedTimesToExit;

    internal BandSurvival(int[] sortedTimesToExit, int censoredCount)
    {
        _sortedTimesToExit = sortedTimesToExit;
        CensoredCount = censoredCount;
    }

    /// <summary>Number of entries that reached the end of the series without exiting the band.</summary>
    public int CensoredCount { get; }

    /// <summary>Number of observed (uncensored) exits.</summary>
    public int ObservedCount => _sortedTimesToExit?.Length ?? 0;

    /// <summary>The observed times-to-exit (steps), ascending.</summary>
    public IReadOnlyList<int> TimesToExit => _sortedTimesToExit ?? [];

    /// <summary>The median observed time-to-exit (steps).</summary>
    public decimal Median() => Quantile(0.5m);

    /// <summary>The <paramref name="q"/>-quantile (linear interpolation) of observed times-to-exit, in steps.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="q"/> is outside [0, 1].</exception>
    /// <exception cref="InvalidOperationException">There are no observed exits.</exception>
    public decimal Quantile(decimal q)
    {
        if (q < 0m || q > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(q), q, "Quantile must be in [0, 1].");
        }

        if (ObservedCount == 0)
        {
            throw new InvalidOperationException("No observed exits to take a quantile of.");
        }

        if (ObservedCount == 1)
        {
            return _sortedTimesToExit[0];
        }

        var position = q * (ObservedCount - 1);
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        var fraction = position - lower;
        return _sortedTimesToExit[lower] + (_sortedTimesToExit[upper] - _sortedTimesToExit[lower]) * fraction;
    }
}
