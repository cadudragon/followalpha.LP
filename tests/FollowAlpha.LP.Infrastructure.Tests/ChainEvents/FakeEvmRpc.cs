using FollowAlpha.LP.Application.ChainEvents;
using FollowAlpha.LP.Infrastructure.ChainEvents;
using Nethereum.RPC.Eth.DTOs;

namespace FollowAlpha.LP.Infrastructure.Tests.ChainEvents;

/// <summary>
/// Offline <see cref="IEvmRpc"/>: returns ABI-correct raw logs (built by <see cref="NpmLogFactory"/>)
/// keyed by event topic0, plus recorded gas and block timestamps. The reader's real Nethereum log→DTO
/// decode runs on these — no live network, no RPC key.
/// </summary>
internal sealed class FakeEvmRpc : IEvmRpc
{
    private readonly Dictionary<string, List<FilterLog>> _logsByTopic = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, GasInfo> GasByTx { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<long, DateTimeOffset> TimestampByBlock { get; } = [];

    public List<string> GasRequests { get; } = [];

    public List<long> TimestampRequests { get; } = [];

    public List<(string Topic, IReadOnlyCollection<string> TokenIds)> LogRequests { get; } = [];

    // Discovery + state-read fakes (the use cases fake the Application ports; these satisfy IEvmRpc and
    // back the event reader's discovery / GetChainHeadBlockAsync unit coverage). Inbound = Transfer(to=wallet),
    // outbound = Transfer(from=wallet); the reader queries each direction with its own filter.
    public List<FilterLog> InboundTransferLogs { get; } = [];

    public List<FilterLog> OutboundTransferLogs { get; } = [];

    public List<(string? From, string? To)> TransferRequests { get; } = [];

    public long LatestBlock { get; set; }

    public Dictionary<string, ChainPositionState> PositionsByToken { get; } = [];

    public Dictionary<string, string> PoolByKey { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, int> DecimalsByToken { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void AddLog(string topic0, FilterLog log)
    {
        if (!_logsByTopic.TryGetValue(topic0, out var logs))
        {
            logs = [];
            _logsByTopic[topic0] = logs;
        }

        logs.Add(log);
    }

    public Task<IReadOnlyList<FilterLog>> GetLogsAsync(
        string chainId, string address, string eventSignatureTopic, IReadOnlyCollection<string> tokenIds, long fromBlock, long toBlock, CancellationToken cancellationToken)
    {
        LogRequests.Add((eventSignatureTopic, tokenIds));
        IReadOnlyList<FilterLog> logs = _logsByTopic.TryGetValue(eventSignatureTopic, out var found) ? found : [];
        return Task.FromResult(logs);
    }

    public Task<GasInfo> GetGasAsync(string chainId, string txHash, CancellationToken cancellationToken)
    {
        GasRequests.Add(txHash);
        return Task.FromResult(GasByTx[txHash]);
    }

    public Task<DateTimeOffset> GetBlockTimestampAsync(string chainId, long blockNumber, CancellationToken cancellationToken)
    {
        TimestampRequests.Add(blockNumber);
        return Task.FromResult(TimestampByBlock[blockNumber]);
    }

    public Task<IReadOnlyList<FilterLog>> GetTransferLogsAsync(
        string chainId, string address, string? fromAddress, string? toAddress, long fromBlock, long toBlock, CancellationToken cancellationToken)
    {
        TransferRequests.Add((fromAddress, toAddress));
        // The reader uses toAddress for acquisitions (inbound) and fromAddress for releases (outbound).
        IReadOnlyList<FilterLog> logs = toAddress is not null ? InboundTransferLogs : OutboundTransferLogs;
        return Task.FromResult(logs);
    }

    public Task<long> GetLatestBlockNumberAsync(string chainId, CancellationToken cancellationToken) =>
        Task.FromResult(LatestBlock);

    public Task<ChainPositionState> GetPositionsAsync(string chainId, string positionManagerAddress, string tokenId, CancellationToken cancellationToken) =>
        Task.FromResult(PositionsByToken[tokenId]);

    public Task<string> GetPoolAddressAsync(string chainId, string factoryAddress, string token0, string token1, int feeTier, CancellationToken cancellationToken) =>
        Task.FromResult(PoolByKey[$"{token0}:{token1}:{feeTier}"]);

    public Task<int> GetTokenDecimalsAsync(string chainId, string tokenAddress, CancellationToken cancellationToken) =>
        Task.FromResult(DecimalsByToken[tokenAddress]);
}
