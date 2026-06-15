namespace FollowAlpha.LP.Domain.Primitives;

/// <summary>
/// The raw pool price — token1 per token0 in base units, the ratio a tick encodes — as an
/// analytics-grade <see cref="decimal"/>. This is the only price type that converts to a tick, and it
/// is reached from a <see cref="HumanPrice"/> only after applying <see cref="TokenDecimals"/> scaling,
/// which is what makes <c>new HumanPrice(2000).ToTick()</c> (no decimals) impossible to express.
/// </summary>
public readonly record struct PoolPrice
{
    /// <summary>Constructs a raw pool price.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rawToken1PerToken0"/> is not strictly positive.</exception>
    public PoolPrice(decimal rawToken1PerToken0)
    {
        if (rawToken1PerToken0 <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rawToken1PerToken0), rawToken1PerToken0, "Pool price must be strictly positive.");
        }

        RawToken1PerToken0 = rawToken1PerToken0;
    }

    /// <summary>The raw token1/token0 ratio in base units.</summary>
    public decimal RawToken1PerToken0 { get; }

    /// <summary>The greatest tick whose price is ≤ this pool price (Uniswap floor semantics).</summary>
    public Tick ToTick() => new(PriceMath.PoolPriceToTick(RawToken1PerToken0));

    /// <summary>This raw price as a human-readable price in the requested orientation, given the tokens' decimals.</summary>
    public HumanPrice ToHumanPrice(
        TokenDecimals decimals,
        PriceOrientation orientation = PriceOrientation.Token1PerToken0)
    {
        var canonical = PriceMath.RawPriceToCanonicalHuman(RawToken1PerToken0, decimals);
        var canonicalHuman = new HumanPrice(canonical, PriceOrientation.Token1PerToken0);
        return orientation == PriceOrientation.Token1PerToken0 ? canonicalHuman : canonicalHuman.Invert();
    }
}
