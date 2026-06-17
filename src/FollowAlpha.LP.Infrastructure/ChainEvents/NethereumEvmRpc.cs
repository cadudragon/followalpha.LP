using System.Globalization;
using System.Numerics;
using FollowAlpha.LP.Application.ChainEvents;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;

namespace FollowAlpha.LP.Infrastructure.ChainEvents;

/// <summary>
/// Production <see cref="IEvmRpc"/> over Nethereum. One <see cref="IWeb3"/> per chain, built by an injected
/// factory (from the configured RPC URL via <see cref="FromOptions"/>). <c>eth_getLogs</c> is filtered by
/// event signature (topic0) and the indexed tokenIds (topic1) so only the requested positions' logs are
/// fetched. The factory seam lets tests drive the real decode path over a transport-level recorded
/// JSON-RPC client (no network), while production builds an <see cref="Web3"/> from the URL.
/// </summary>
public sealed class NethereumEvmRpc(Func<string, IWeb3> web3Factory, long maxBlockSpan = 50_000) : IEvmRpc
{
    // keccak("Transfer(address,address,uint256)") — the ERC-721 Transfer topic0.
    private const string TransferTopic = "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef";

    private readonly Dictionary<string, IWeb3> _web3ByChain = new(StringComparer.OrdinalIgnoreCase);
    private readonly long _maxBlockSpan = maxBlockSpan > 0 ? maxBlockSpan : 50_000;

    /// <summary>Production factory: one <see cref="Web3"/> per chain built from the configured RPC URL.</summary>
    public static NethereumEvmRpc FromOptions(EvmRpcOptions options) =>
        new(
            chainId => options.RpcUrls.TryGetValue(chainId, out var url) && !string.IsNullOrWhiteSpace(url)
                ? new Web3(url)
                : throw new KeyNotFoundException($"No RPC URL configured for chain '{chainId}'."),
            options.MaxBlockSpan);

    public Task<IReadOnlyList<FilterLog>> GetLogsAsync(
        string chainId, string address, string eventSignatureTopic, IReadOnlyCollection<string> tokenIds, long fromBlock, long toBlock, CancellationToken cancellationToken)
    {
        object?[] topics = [eventSignatureTopic, tokenIds.Select(ToTopicHex).ToArray()];
        return GetLogsChunkedAsync(chainId, topics, address, fromBlock, toBlock, cancellationToken);
    }

    public Task<IReadOnlyList<FilterLog>> GetTransferLogsAsync(
        string chainId, string address, string? fromAddress, string? toAddress, long fromBlock, long toBlock, CancellationToken cancellationToken)
    {
        // Topics: [Transfer, from (topic1, null = wildcard), to (topic2, null = wildcard)]; tokenId is topic3.
        object?[] topics = [TransferTopic, NullableAddressTopic(fromAddress), NullableAddressTopic(toAddress)];
        return GetLogsChunkedAsync(chainId, topics, address, fromBlock, toBlock, cancellationToken);
    }

    /// <summary>
    /// Runs <c>eth_getLogs</c> over <c>[fromBlock, toBlock]</c> in spans of at most <see cref="_maxBlockSpan"/>
    /// blocks and concatenates the results, so a wide range (e.g. <c>fromBlock=0</c>) never exceeds provider
    /// range/response limits. Chunks are contiguous and inclusive; ordering is preserved across chunks.
    /// </summary>
    private async Task<IReadOnlyList<FilterLog>> GetLogsChunkedAsync(
        string chainId, object?[] topics, string address, long fromBlock, long toBlock, CancellationToken cancellationToken)
    {
        var web3 = GetWeb3(chainId);
        var all = new List<FilterLog>();

        for (var start = fromBlock; start <= toBlock; start += _maxBlockSpan)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var end = Math.Min(start + _maxBlockSpan - 1, toBlock);
            var filter = new NewFilterInput
            {
                Address = [address],
                // Nethereum's Topics is object[]; null entries are wildcard topic positions by design.
                Topics = topics!,
                FromBlock = new BlockParameter(new HexBigInteger(start)),
                ToBlock = new BlockParameter(new HexBigInteger(end)),
            };

            all.AddRange(await web3.Eth.Filters.GetLogs.SendRequestAsync(filter));
        }

        return all;
    }

    public async Task<ChainPositionState> GetPositionsAsync(string chainId, string positionManagerAddress, string tokenId, CancellationToken cancellationToken)
    {
        var handler = GetWeb3(chainId).Eth.GetContractQueryHandler<PositionsFunction>();
        var p = await handler.QueryDeserializingToObjectAsync<PositionsOutputDto>(
            new PositionsFunction { TokenId = BigInteger.Parse(tokenId, CultureInfo.InvariantCulture) }, positionManagerAddress);
        return new ChainPositionState(p.TickLower, p.TickUpper, p.Token0.ToLowerInvariant(), p.Token1.ToLowerInvariant(), (int)p.Fee);
    }

    public async Task<string> GetPoolAddressAsync(string chainId, string factoryAddress, string token0, string token1, int feeTier, CancellationToken cancellationToken)
    {
        var handler = GetWeb3(chainId).Eth.GetContractQueryHandler<GetPoolFunction>();
        var pool = await handler.QueryAsync<string>(factoryAddress,
            new GetPoolFunction { TokenA = token0, TokenB = token1, Fee = (uint)feeTier });
        return pool.ToLowerInvariant();
    }

    public async Task<int> GetTokenDecimalsAsync(string chainId, string tokenAddress, CancellationToken cancellationToken)
    {
        var handler = GetWeb3(chainId).Eth.GetContractQueryHandler<DecimalsFunction>();
        return (int)await handler.QueryAsync<BigInteger>(tokenAddress, new DecimalsFunction());
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

    public async Task<long> GetLatestBlockNumberAsync(string chainId, CancellationToken cancellationToken)
    {
        var head = await GetWeb3(chainId).Eth.Blocks.GetBlockNumber.SendRequestAsync();
        return (long)head.Value;
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

    // A 20-byte address left-padded into a 32-byte, 0x-prefixed log topic; null stays null (topic wildcard).
    private static string? NullableAddressTopic(string? address)
    {
        if (string.IsNullOrEmpty(address))
        {
            return null;
        }

        var hex = address.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? address[2..] : address;
        return "0x" + new string('0', 64 - hex.Length) + hex.ToLowerInvariant();
    }

    private IWeb3 GetWeb3(string chainId)
    {
        if (_web3ByChain.TryGetValue(chainId, out var existing))
        {
            return existing;
        }

        var web3 = web3Factory(chainId);
        _web3ByChain[chainId] = web3;
        return web3;
    }
}
