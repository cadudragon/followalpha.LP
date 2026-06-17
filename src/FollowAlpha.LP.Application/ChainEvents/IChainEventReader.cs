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

    /// <summary>
    /// The distinct position <c>tokenId</c>s the wallet has received (NPM ERC-721 <c>Transfer(to=wallet)</c>)
    /// in <c>[fromBlock, toBlock]</c> on <paramref name="chainId"/>, ascending. A tokenId later transferred
    /// out is still returned — owner-at-time attribution is the caller's concern (see
    /// <see cref="DiscoverWalletTransfersAsync"/>).
    /// </summary>
    Task<IReadOnlyList<string>> DiscoverWalletTokenIdsAsync(
        string chainId,
        string walletAddress,
        long fromBlock,
        long toBlock,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// All NPM ERC-721 <c>Transfer</c> logs touching the wallet (as recipient <b>and</b> as sender) in
    /// <c>[fromBlock, toBlock]</c>, carrying the tokenId and the exact <c>(block, logIndex)</c> and
    /// direction — the raw material for building owner-at-time intervals so a position transferred out is
    /// not mis-attributed in the append-only audit truth.
    /// </summary>
    Task<IReadOnlyList<WalletNftTransfer>> DiscoverWalletTransfersAsync(
        string chainId,
        string walletAddress,
        long fromBlock,
        long toBlock,
        CancellationToken cancellationToken = default);

    /// <summary>The latest block number on <paramref name="chainId"/> — the upper bound for a sync window.</summary>
    Task<long> GetChainHeadBlockAsync(string chainId, CancellationToken cancellationToken = default);
}

/// <summary>Whether a wallet acquired (<see cref="In"/>) or released (<see cref="Out"/>) a position NFT.</summary>
public enum TransferDirection
{
    In,
    Out,
}

/// <summary>One NPM ERC-721 <c>Transfer</c> touching a wallet, with the precise ordering needed for ownership windows.</summary>
public sealed record WalletNftTransfer(
    string TokenId,
    long BlockNumber,
    int LogIndex,
    TransferDirection Direction);
