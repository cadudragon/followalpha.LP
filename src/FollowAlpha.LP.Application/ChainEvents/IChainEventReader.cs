namespace FollowAlpha.LP.Application.ChainEvents;

/// <summary>
/// Reads raw NonfungiblePositionManager events (mint/burn/collect) with native gas over a block range,
/// for a chain (ARCHITECTURE.md §5/§6; Nethereum/RPC in v1). A thin log+receipt reader: it does not
/// resolve ownership, ticks, pool, token decimals, or USD — that enrichment happens downstream.
/// </summary>
public interface IChainEventReader
{
    /// <summary>
    /// All NPM position events in <c>[fromBlock, toBlock]</c> on <paramref name="chainId"/>, ordered by
    /// (block, log index). Each event carries its <c>TokenId</c> so the caller can attribute it to a
    /// configured wallet during enrichment.
    /// </summary>
    Task<IReadOnlyList<ChainPositionEvent>> ReadPositionEventsAsync(
        string chainId,
        long fromBlock,
        long toBlock,
        CancellationToken cancellationToken = default);
}
