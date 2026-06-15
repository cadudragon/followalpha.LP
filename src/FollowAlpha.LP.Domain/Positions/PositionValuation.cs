namespace FollowAlpha.LP.Domain.Positions;

/// <summary>
/// A position (or benchmark) holding at a price: token0 amount, token1 amount, and total value in the
/// token1 numeraire (<c>Value = AmountY + AmountX·price</c>). All quantities are analytics-grade
/// <see cref="decimal"/> in raw pool terms (token1/token0), consistent with the kernel.
/// </summary>
public readonly record struct PositionValuation(decimal AmountX, decimal AmountY, decimal Value);
