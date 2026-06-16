using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;

namespace FollowAlpha.LP.Infrastructure.ChainEvents;

/// <summary>
/// Production <see cref="IEvmRpc"/> over Nethereum. One <see cref="IWeb3"/> per chain, built from the
/// configured RPC URL. HTTP resilience (retry/backoff) is attached at the composition root (Phase 2.4).
/// </summary>
public sealed class NethereumEvmRpc(EvmRpcOptions options) : IEvmRpc
{
    private readonly Dictionary<string, IWeb3> _web3ByChain = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<EventLog<TEventDto>>> GetEventsAsync<TEventDto>(
        string chainId, string contractAddress, long fromBlock, long toBlock, CancellationToken cancellationToken)
        where TEventDto : class, IEventDTO, new()
    {
        var contractEvent = GetWeb3(chainId).Eth.GetEvent<TEventDto>(contractAddress);
        var filter = contractEvent.CreateFilterInput(
            new BlockParameter(new HexBigInteger(fromBlock)),
            new BlockParameter(new HexBigInteger(toBlock)));
        return await contractEvent.GetAllChangesAsync(filter);
    }

    public async Task<GasInfo> GetGasAsync(string chainId, string txHash, CancellationToken cancellationToken)
    {
        var receipt = await GetWeb3(chainId).Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);
        var gasUsed = receipt.GasUsed?.Value ?? BigInteger.Zero;
        var effectiveGasPrice = receipt.EffectiveGasPrice?.Value ?? BigInteger.Zero;
        return new GasInfo(gasUsed, effectiveGasPrice);
    }

    public async Task<DateTimeOffset> GetBlockTimestampAsync(string chainId, long blockNumber, CancellationToken cancellationToken)
    {
        var block = await GetWeb3(chainId).Eth.Blocks.GetBlockWithTransactionsHashesByNumber
            .SendRequestAsync(new BlockParameter(new HexBigInteger(blockNumber)));
        return DateTimeOffset.FromUnixTimeSeconds((long)block.Timestamp.Value);
    }

    private IWeb3 GetWeb3(string chainId)
    {
        if (_web3ByChain.TryGetValue(chainId, out var existing))
        {
            return existing;
        }

        if (!options.RpcUrls.TryGetValue(chainId, out var url) || string.IsNullOrWhiteSpace(url))
        {
            throw new KeyNotFoundException($"No RPC URL configured for chain '{chainId}'.");
        }

        var web3 = new Web3(url);
        _web3ByChain[chainId] = web3;
        return web3;
    }
}
