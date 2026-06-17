using System.Globalization;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;

namespace FollowAlpha.LP.Infrastructure.ChainEvents;

/// <summary>
/// Production <see cref="IEvmRpc"/> over Nethereum. One <see cref="IWeb3"/> per chain, built from the
/// configured RPC URL. <c>eth_getLogs</c> is filtered by event signature (topic0) and the indexed
/// tokenIds (topic1) so only the requested positions' logs are fetched. HTTP resilience (retry/backoff)
/// is attached at the composition root (Phase 2.4).
/// </summary>
public sealed class NethereumEvmRpc(EvmRpcOptions options) : IEvmRpc
{
    private readonly Dictionary<string, IWeb3> _web3ByChain = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<FilterLog>> GetLogsAsync(
        string chainId, string address, string eventSignatureTopic, IReadOnlyCollection<string> tokenIds, long fromBlock, long toBlock, CancellationToken cancellationToken)
    {
        var tokenIdTopics = tokenIds.Select(ToTopicHex).ToArray();
        var filter = new NewFilterInput
        {
            Address = [address],
            Topics = [eventSignatureTopic, tokenIdTopics],
            FromBlock = new BlockParameter(new HexBigInteger(fromBlock)),
            ToBlock = new BlockParameter(new HexBigInteger(toBlock)),
        };

        return await GetWeb3(chainId).Eth.Filters.GetLogs.SendRequestAsync(filter);
    }

    public async Task<GasInfo> GetGasAsync(string chainId, string txHash, CancellationToken cancellationToken)
    {
        var receipt = await GetWeb3(chainId).Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);
        var gasUsed = receipt.GasUsed?.Value ?? BigInteger.Zero;
        BigInteger? effectiveGasPrice = receipt.EffectiveGasPrice is { } price ? price.Value : null;
        return new GasInfo(gasUsed, effectiveGasPrice);
    }

    public async Task<DateTimeOffset> GetBlockTimestampAsync(string chainId, long blockNumber, CancellationToken cancellationToken)
    {
        var block = await GetWeb3(chainId).Eth.Blocks.GetBlockWithTransactionsHashesByNumber
            .SendRequestAsync(new BlockParameter(new HexBigInteger(blockNumber)));
        return DateTimeOffset.FromUnixTimeSeconds((long)block.Timestamp.Value);
    }

    // A uint256 tokenId as a 32-byte, 0x-prefixed log topic.
    private static string ToTopicHex(string tokenId)
    {
        var value = BigInteger.Parse(tokenId, CultureInfo.InvariantCulture);
        var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        var padded = new byte[32];
        Array.Copy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);
        return "0x" + Convert.ToHexStringLower(padded);
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
