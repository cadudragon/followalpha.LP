namespace FollowAlpha.LP.Application.Pools;

/// <summary>
/// The canonical <c>Pool.Id</c> = chain id + pool address (DATA-MODEL.md §2), built in one place so the
/// Collector's snapshot path and the wallet-sync enrichment path produce identical ids for the same pool.
/// </summary>
public static class PoolIdentity
{
    /// <summary>The pool id for a chain + pool address (address lower-cased).</summary>
    public static string For(string chainId, string poolAddress) =>
        $"{chainId}:{poolAddress.ToLowerInvariant()}";
}
