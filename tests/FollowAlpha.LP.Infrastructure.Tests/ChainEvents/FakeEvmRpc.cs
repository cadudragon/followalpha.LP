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
}
