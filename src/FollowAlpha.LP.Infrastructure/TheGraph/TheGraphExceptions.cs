namespace FollowAlpha.LP.Infrastructure.TheGraph;

/// <summary>Thrown when The Graph returns GraphQL errors or no usable data.</summary>
public sealed class TheGraphQueryException : Exception
{
    public TheGraphQueryException(string message)
        : base(message)
    {
    }

    public TheGraphQueryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when a requested pool is not present in the subgraph.</summary>
public sealed class PoolNotFoundException(string chainId, string poolAddress)
    : Exception($"Pool '{poolAddress}' was not found on chain '{chainId}'.")
{
    public string ChainId { get; } = chainId;

    public string PoolAddress { get; } = poolAddress;
}
