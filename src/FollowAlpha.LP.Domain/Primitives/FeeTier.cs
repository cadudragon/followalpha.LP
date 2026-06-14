namespace FollowAlpha.LP.Domain.Primitives;

/// <summary>
/// A Uniswap v3 fee tier, identified by its fee in pips (hundredths of a basis point) and carrying the
/// tier's initialized tick spacing. Only the four canonical tiers exist; any other value is rejected.
/// (Forks with bespoke tiers are a descriptor concern in Infrastructure, not a Domain primitive.)
/// </summary>
public readonly record struct FeeTier
{
    private FeeTier(int feePips) => FeePips = feePips;

    /// <summary>0.01% tier — tick spacing 1.</summary>
    public static readonly FeeTier Stable = new(100);

    /// <summary>0.05% tier — tick spacing 10.</summary>
    public static readonly FeeTier Low = new(500);

    /// <summary>0.30% tier — tick spacing 60.</summary>
    public static readonly FeeTier Medium = new(3000);

    /// <summary>1.00% tier — tick spacing 200.</summary>
    public static readonly FeeTier High = new(10000);

    /// <summary>The fee in pips (hundredths of a basis point): one of 100, 500, 3000, 10000.</summary>
    public int FeePips { get; }

    /// <summary>The fee as a fraction of notional (e.g. 0.0005 for the 0.05% tier).</summary>
    public decimal FeeFraction => FeePips / 1_000_000m;

    /// <summary>The tier's initialized tick spacing.</summary>
    /// <exception cref="InvalidOperationException">The instance is not one of the four canonical tiers (e.g. <c>default</c>).</exception>
    public int TickSpacing => TickSpacingFor(FeePips);

    /// <summary>Resolves a fee tier from its pip value.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="feePips"/> is not a canonical tier.</exception>
    public static FeeTier FromPips(int feePips) => feePips switch
    {
        100 => Stable,
        500 => Low,
        3000 => Medium,
        10000 => High,
        _ => throw new ArgumentOutOfRangeException(nameof(feePips), feePips, "Not a canonical Uniswap v3 fee tier."),
    };

    // Kept out of the property getter so the throw is not flagged as an unexpected getter exception.
    private static int TickSpacingFor(int feePips) => feePips switch
    {
        100 => 1,
        500 => 10,
        3000 => 60,
        10000 => 200,
        _ => throw new InvalidOperationException("Not a canonical Uniswap v3 fee tier."),
    };
}
