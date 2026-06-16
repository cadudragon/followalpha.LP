using System.Globalization;
using System.Numerics;
using FollowAlpha.LP.Application.ChainEvents;
using FollowAlpha.LP.Application.Protocols;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;

namespace FollowAlpha.LP.Infrastructure.ChainEvents;

/// <summary>
/// <see cref="IChainEventReader"/> over EVM RPC (ARCHITECTURE.md §6). Reads the NonfungiblePositionManager
/// IncreaseLiquidity/DecreaseLiquidity/Collect logs for a block range, maps them to raw
/// <see cref="ChainPositionEvent"/>s (signed liquidity delta, native gas), and leaves all enrichment to
/// the caller. The NPM address per chain comes from the descriptor registry. Decoding/RPC is the
/// injected <see cref="IEvmRpc"/> seam.
/// </summary>
public sealed class EvmRpcEventReader(IDexProtocolRegistry registry, IEvmRpc rpc) : IChainEventReader
{
    public async Task<IReadOnlyList<ChainPositionEvent>> ReadPositionEventsAsync(
        string chainId, long fromBlock, long toBlock, CancellationToken cancellationToken = default)
    {
        var positionManager = registry.GetByChain(chainId).PositionManagerAddress;
        var gasByTx = new Dictionary<string, GasInfo>(StringComparer.OrdinalIgnoreCase);
        var timestampByBlock = new Dictionary<long, DateTimeOffset>();
        var results = new List<ChainPositionEvent>();

        var increases = await rpc.GetEventsAsync<IncreaseLiquidityEventDto>(chainId, positionManager, fromBlock, toBlock, cancellationToken);
        foreach (var e in increases)
        {
            results.Add(await MapAsync(chainId, positionManager, e.Log, PositionEventTypes.Mint,
                e.Event.Liquidity, e.Event.Amount0, e.Event.Amount1, e.Event.TokenId, recipient: null, gasByTx, timestampByBlock, cancellationToken));
        }

        var decreases = await rpc.GetEventsAsync<DecreaseLiquidityEventDto>(chainId, positionManager, fromBlock, toBlock, cancellationToken);
        foreach (var e in decreases)
        {
            results.Add(await MapAsync(chainId, positionManager, e.Log, PositionEventTypes.Burn,
                BigInteger.Negate(e.Event.Liquidity), e.Event.Amount0, e.Event.Amount1, e.Event.TokenId, recipient: null, gasByTx, timestampByBlock, cancellationToken));
        }

        var collects = await rpc.GetEventsAsync<CollectEventDto>(chainId, positionManager, fromBlock, toBlock, cancellationToken);
        foreach (var e in collects)
        {
            results.Add(await MapAsync(chainId, positionManager, e.Log, PositionEventTypes.Collect,
                BigInteger.Zero, e.Event.Amount0, e.Event.Amount1, e.Event.TokenId, e.Event.Recipient, gasByTx, timestampByBlock, cancellationToken));
        }

        return [.. results.OrderBy(r => r.BlockNumber).ThenBy(r => r.LogIndex)];
    }

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
            EffectiveGasPriceWei: gas.EffectiveGasPriceWei.ToString(CultureInfo.InvariantCulture),
            NativeGasCostWei: gas.NativeCostWei.ToString(CultureInfo.InvariantCulture),
            Recipient: recipient,
            PositionManagerAddress: positionManager);
    }
}
