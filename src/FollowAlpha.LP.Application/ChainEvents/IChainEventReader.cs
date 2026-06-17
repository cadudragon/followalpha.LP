namespace FollowAlpha.LP.Application.ChainEvents;

/// <summary>
/// Reads raw NonfungiblePositionManager events (mint/burn/collect) with native gas, scoped to specific
/// position <c>tokenId</c>s over a block range, for a chain (ARCHITECTURE.md §5/§6; Nethereum/RPC in v1).
/// A thin log+receipt reader: it does not resolve ownership, ticks, pool, token decimals, or USD — that
/// enrichment happens downstream. The caller resolves which tokenIds belong to its configured wallets
/// (via NPM <c>Transfer</c>) during enrichment and passes them here, so the reader filters by indexed
/// <c>tokenId</c> at the RPC rather than scanning the whole position manager.
/// </summary>
public interface IChainEventReader
{
    /// <summary>
    /// The NPM position events for the given <paramref name="tokenIds"/> in <c>[fromBlock, toBlock]</c> on
    /// <paramref name="chainId"/>, ordered by (block, log index). Returns empty when no tokenIds are given
    /// (never a global scan).
    /// </summary>
    Task<IReadOnlyList<ChainPositionEvent>> ReadPositionEventsAsync(
        string chainId,
        IReadOnlyCollection<string> tokenIds,
        long fromBlock,
        long toBlock,
        CancellationToken cancellationToken = default);
}
