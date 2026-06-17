namespace FollowAlpha.LP.Application.Pools;

/// <summary>
/// The canonical <c>Asset.Id</c> = chain id + token contract address (DATA-MODEL.md §2), built in one
/// place so seeding and the price-series ingestion produce identical ids for the same token. The id is
/// <b>chain-aware</b> (decided 2026-06-17): the same symbol (e.g. <c>WETH</c>) has different addresses on
/// Arbitrum and Base, so a symbol-only id would collide. Mirrors <see cref="PoolIdentity"/>.
/// </summary>
public static class AssetIdentity
{
    /// <summary>The asset id for a chain + token address (address lower-cased).</summary>
    public static string For(string chainId, string tokenAddress) =>
        $"{chainId}:{tokenAddress.ToLowerInvariant()}";
}
