namespace FollowAlpha.LP.Domain.Primitives;

/// <summary>
/// Which ratio a <see cref="HumanPrice"/> expresses. The canonical orientation is the one tick and
/// <c>sqrtPriceX96</c> are defined in; the inverse is the human reading of the other token.
/// Orientation is carried explicitly so inversion never silently swaps floor↔ceiling or lower↔upper
/// (ARCHITECTURE.md §4.1).
/// </summary>
public enum PriceOrientation
{
    /// <summary>token1 per token0 — the canonical orientation of tick and <c>sqrtPriceX96</c>.</summary>
    Token1PerToken0,

    /// <summary>token0 per token1 — the reciprocal of the canonical price.</summary>
    Token0PerToken1,
}
