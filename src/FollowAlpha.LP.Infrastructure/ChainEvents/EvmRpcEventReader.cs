using System.Globalization;
using System.Numerics;
using FollowAlpha.LP.Application.ChainEvents;
using FollowAlpha.LP.Application.Protocols;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace FollowAlpha.LP.Infrastructure.ChainEvents;

/// <summary>
/// <see cref="IChainEventReader"/> over EVM RPC (ARCHITECTURE.md §6). For the requested position
/// <c>tokenId</c>s it reads the NonfungiblePositionManager IncreaseLiquidity/DecreaseLiquidity/Collect
/// logs (filtered by indexed tokenId — never a global scan), <b>decodes</b> them, and maps to raw
/// <see cref="ChainPositionEvent"/>s (signed liquidity delta, native gas; null gas when unknown). All
/// enrichment is left to the caller. Raw logs/receipts come through the injected <see cref="IEvmRpc"/>.
/// </summary>
public sealed class EvmRpcEventReader(IDexProtocolRegistry registry, IEvmRpc rpc) : IChainEventReader
{
    public async Task<IReadOnlyList<ChainPositionEvent>> ReadPositionEventsAsync(
        string chainId, IReadOnlyCollection<string> tokenIds, long fromBlock, long toBlock, CancellationToken cancellationToken = default)
    {
        if (fromBlock > toBlock)
        {
            throw new ArgumentException("fromBlock must be less than or equal to toBlock.", nameof(fromBlock));
        }

        if (tokenIds is null || tokenIds.Count == 0)
        {
            return []; // never a global scan: no tokenIds → no events
        }

        var positionManager = registry.GetByChain(chainId).PositionManagerAddress;
        var gasByTx = new Dictionary<string, GasInfo>(StringComparer.OrdinalIgnoreCase);
        var timestampByBlock = new Dictionary<long, DateTimeOffset>();
        var results = new List<ChainPositionEvent>();

        foreach (var log in await GetLogsAsync<IncreaseLiquidityEventDto>(chainId, positionManager, tokenIds, fromBlock, toBlock, cancellationToken))
        {
            var decoded = log.DecodeEvent<IncreaseLiquidityEventDto>();
            if (decoded is null)
            {
                continue;
            }

            results.Add(await MapAsync(chainId, positionManager, decoded.Log, PositionEventTypes.Mint,
                decoded.Event.Liquidity, decoded.Event.Amount0, decoded.Event.Amount1, decoded.Event.TokenId, recipient: null, gasByTx, timestampByBlock, cancellationToken));
        }

        foreach (var log in await GetLogsAsync<DecreaseLiquidityEventDto>(chainId, positionManager, tokenIds, fromBlock, toBlock, cancellationToken))
        {
            var decoded = log.DecodeEvent<DecreaseLiquidityEventDto>();
            if (decoded is null)
            {
                continue;
            }

            results.Add(await MapAsync(chainId, positionManager, decoded.Log, PositionEventTypes.Burn,
                BigInteger.Negate(decoded.Event.Liquidity), decoded.Event.Amount0, decoded.Event.Amount1, decoded.Event.TokenId, recipient: null, gasByTx, timestampByBlock, cancellationToken));
        }

        foreach (var log in await GetLogsAsync<CollectEventDto>(chainId, positionManager, tokenIds, fromBlock, toBlock, cancellationToken))
        {
            var decoded = log.DecodeEvent<CollectEventDto>();
            if (decoded is null)
            {
                continue;
            }

            results.Add(await MapAsync(chainId, positionManager, decoded.Log, PositionEventTypes.Collect,
                BigInteger.Zero, decoded.Event.Amount0, decoded.Event.Amount1, decoded.Event.TokenId, decoded.Event.Recipient, gasByTx, timestampByBlock, cancellationToken));
        }

        return [.. results.OrderBy(r => r.BlockNumber).ThenBy(r => r.LogIndex)];
    }

    public async Task<IReadOnlyList<string>> DiscoverWalletTokenIdsAsync(
        string chainId, string walletAddress, long fromBlock, long toBlock, CancellationToken cancellationToken = default)
    {
        if (fromBlock > toBlock)
        {
            throw new ArgumentException("fromBlock must be less than or equal to toBlock.", nameof(fromBlock));
        }

        var positionManager = registry.GetByChain(chainId).PositionManagerAddress;
        var logs = await rpc.GetTransferLogsAsync(chainId, positionManager, fromAddress: null, toAddress: walletAddress, fromBlock, toBlock, cancellationToken);

        // The tokenId is the third indexed topic of an ERC-721 Transfer.
        return [.. logs
            .Select(log => new HexBigInteger(log.Topics[3].ToString()!).Value)
            .Distinct()
            .OrderBy(id => id)
            .Select(id => id.ToString(CultureInfo.InvariantCulture))];
    }

    public async Task<IReadOnlyList<WalletNftTransfer>> DiscoverWalletTransfersAsync(
        string chainId, string walletAddress, long fromBlock, long toBlock, CancellationToken cancellationToken = default)
    {
        if (fromBlock > toBlock)
        {
            throw new ArgumentException("fromBlock must be less than or equal to toBlock.", nameof(fromBlock));
        }

        var positionManager = registry.GetByChain(chainId).PositionManagerAddress;

        // Two filters: a single eth_getLogs ANDs topic positions, so "to == wallet" and "from == wallet"
        // are separate queries (acquisitions vs releases). A self-transfer would appear in both; harmless —
        // the ownership fold dedupes by (block, logIndex).
        var inbound = await rpc.GetTransferLogsAsync(chainId, positionManager, fromAddress: null, toAddress: walletAddress, fromBlock, toBlock, cancellationToken);
        var outbound = await rpc.GetTransferLogsAsync(chainId, positionManager, fromAddress: walletAddress, toAddress: null, fromBlock, toBlock, cancellationToken);

        var transfers = new List<WalletNftTransfer>(inbound.Count + outbound.Count);
        transfers.AddRange(inbound.Select(log => ToTransfer(log, TransferDirection.In)));
        transfers.AddRange(outbound.Select(log => ToTransfer(log, TransferDirection.Out)));

        return [.. transfers.OrderBy(t => t.BlockNumber).ThenBy(t => t.LogIndex)];
    }

    private static WalletNftTransfer ToTransfer(FilterLog log, TransferDirection direction) => new(
        TokenId: new HexBigInteger(log.Topics[3].ToString()!).Value.ToString(CultureInfo.InvariantCulture),
        BlockNumber: (long)log.BlockNumber.Value,
        LogIndex: (int)log.LogIndex.Value,
        Direction: direction);

    public Task<long> GetChainHeadBlockAsync(string chainId, CancellationToken cancellationToken = default) =>
        rpc.GetLatestBlockNumberAsync(chainId, cancellationToken);

    private Task<IReadOnlyList<FilterLog>> GetLogsAsync<TEventDto>(
        string chainId, string positionManager, IReadOnlyCollection<string> tokenIds, long fromBlock, long toBlock, CancellationToken cancellationToken)
        where TEventDto : class, IEventDTO, new() =>
        rpc.GetLogsAsync(chainId, positionManager, SignatureTopic<TEventDto>(), tokenIds, fromBlock, toBlock, cancellationToken);

    /// <summary>The event's topic0 (keccak of the canonical signature), 0x-prefixed.</summary>
    internal static string SignatureTopic<TEventDto>()
        where TEventDto : class, IEventDTO, new() =>
        "0x" + ABITypedRegistry.GetEvent<TEventDto>().Sha3Signature;

    private async Task<ChainPositionEvent> MapAsync(
        string chainId,
        string positionManager,
        FilterLog log,
        string eventType,
        BigInteger liquidityDelta,
        BigInteger amount0,
        BigInteger amount1,
        BigInteger tokenId,
        string? recipient,
        Dictionary<string, GasInfo> gasByTx,
        Dictionary<long, DateTimeOffset> timestampByBlock,
        CancellationToken cancellationToken)
    {
        var txHash = log.TransactionHash;
        var blockNumber = (long)log.BlockNumber.Value;

        if (!gasByTx.TryGetValue(txHash, out var gas))
        {
            gas = await rpc.GetGasAsync(chainId, txHash, cancellationToken);
            gasByTx[txHash] = gas;
        }

        if (!timestampByBlock.TryGetValue(blockNumber, out var blockTime))
        {
            blockTime = await rpc.GetBlockTimestampAsync(chainId, blockNumber, cancellationToken);
            timestampByBlock[blockNumber] = blockTime;
        }

        return new ChainPositionEvent(
            ChainId: chainId,
            TxHash: txHash,
            LogIndex: (int)log.LogIndex.Value,
            BlockNumber: blockNumber,
            BlockTimeUtc: blockTime,
            TokenId: tokenId.ToString(CultureInfo.InvariantCulture),
            EventType: eventType,
            LiquidityDeltaRaw: liquidityDelta.ToString(CultureInfo.InvariantCulture),
            Amount0Raw: amount0.ToString(CultureInfo.InvariantCulture),
            Amount1Raw: amount1.ToString(CultureInfo.InvariantCulture),
            GasUsed: gas.GasUsed.ToString(CultureInfo.InvariantCulture),
            EffectiveGasPriceWei: gas.EffectiveGasPriceWei?.ToString(CultureInfo.InvariantCulture),
            NativeGasCostWei: gas.NativeCostWei?.ToString(CultureInfo.InvariantCulture),
            Recipient: recipient,
            PositionManagerAddress: positionManager);
    }
}
