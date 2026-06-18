namespace FollowAlpha.LP.Application.ChainEvents;

/// <summary>
/// On-chain position-state reads used to enrich raw <see cref="ChainPositionEvent"/>s into persistable
/// position facts (ARCHITECTURE.md §5/§6). These are read-only contract calls (<c>eth_call</c>): the
/// NonfungiblePositionManager <c>positions(tokenId)</c> (ticks + the token pair + fee tier), the factory's
/// <c>getPool</c> (the pool address), and ERC-20 <c>decimals()</c> (to scale raw amounts to human units).
/// The DataSync worker composes these during wallet sync; the addresses (NPM, factory) resolve from the
/// <c>IDexProtocolRegistry</c> descriptor for the chain.
/// </summary>
public interface IPositionStateReader
{
    /// <summary>The position's range, token pair, and fee tier from NPM <c>positions(tokenId)</c>.</summary>
    Task<ChainPositionState> GetPositionStateAsync(string chainId, string tokenId, CancellationToken cancellationToken = default);

    /// <summary>The pool address for a token pair + fee tier via the factory's <c>getPool</c> (lower-cased).</summary>
    Task<string> GetPoolAddressAsync(string chainId, string token0, string token1, int feeTier, CancellationToken cancellationToken = default);

    /// <summary>The ERC-20 token's <c>decimals()</c> — for scaling raw amounts to human units.</summary>
    Task<int> GetTokenDecimalsAsync(string chainId, string tokenAddress, CancellationToken cancellationToken = default);
}

/// <summary>
/// The subset of NPM <c>positions(tokenId)</c> the enricher needs: the initialized range, the token pair
/// (lower-cased addresses), and the fee tier. Other fields (operator, fee growth, tokensOwed) are not
/// part of the position fact and are intentionally omitted.
/// </summary>
public sealed record ChainPositionState(
    int TickLower,
    int TickUpper,
    string Token0,
    string Token1,
    int FeeTier);
