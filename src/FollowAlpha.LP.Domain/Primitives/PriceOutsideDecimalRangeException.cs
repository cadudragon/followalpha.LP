namespace FollowAlpha.LP.Domain.Primitives;

/// <summary>
/// Thrown when a tick's analytics-grade <see cref="decimal"/> price falls outside the representable
/// decimal window. The raw integer types (<see cref="Tick"/>, <see cref="SqrtPriceX96"/>) still cover
/// the full Uniswap range; it is only the decimal <i>view</i> (<c>1.0001^tick</c>) that is bounded.
/// A distinct domain exception (rather than a bare <see cref="OverflowException"/>) makes that boundary
/// explicit at the call site.
/// </summary>
public sealed class PriceOutsideDecimalRangeException : Exception
{
    public PriceOutsideDecimalRangeException()
    {
    }

    public PriceOutsideDecimalRangeException(string message)
        : base(message)
    {
    }

    public PriceOutsideDecimalRangeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
