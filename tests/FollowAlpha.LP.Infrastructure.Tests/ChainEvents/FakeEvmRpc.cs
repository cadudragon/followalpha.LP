using FollowAlpha.LP.Infrastructure.ChainEvents;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace FollowAlpha.LP.Infrastructure.Tests.ChainEvents;

/// <summary>
/// Offline <see cref="IEvmRpc"/>: returns pre-decoded event logs, gas, and block timestamps from
/// in-memory captures (no live network, no RPC key). The seam sits above Nethereum's log decoding, so
/// fixtures need no hand-authored ABI hex; the wire-level decode is validated against real captures
/// (pending) per OPEN-DECISIONS.md.
/// </summary>
internal sealed class FakeEvmRpc : IEvmRpc
{
    public List<EventLog<IncreaseLiquidityEventDto>> Increases { get; } = [];

    public List<EventLog<DecreaseLiquidityEventDto>> Decreases { get; } = [];

    public List<EventLog<CollectEventDto>> Collects { get; } = [];

    public Dictionary<string, GasInfo> GasByTx { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<long, DateTimeOffset> TimestampByBlock { get; } = [];

    public List<string> GasRequests { get; } = [];

    public List<long> TimestampRequests { get; } = [];

    public Task<IReadOnlyList<EventLog<TEventDto>>> GetEventsAsync<TEventDto>(
        string chainId, string contractAddress, long fromBlock, long toBlock, CancellationToken cancellationToken)
        where TEventDto : class, IEventDTO, new()
    {
        IReadOnlyList<EventLog<TEventDto>> result =
            typeof(TEventDto) == typeof(IncreaseLiquidityEventDto) ? (IReadOnlyList<EventLog<TEventDto>>)(object)Increases :
            typeof(TEventDto) == typeof(DecreaseLiquidityEventDto) ? (IReadOnlyList<EventLog<TEventDto>>)(object)Decreases :
            typeof(TEventDto) == typeof(CollectEventDto) ? (IReadOnlyList<EventLog<TEventDto>>)(object)Collects :
            [];
        return Task.FromResult(result);
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

    public static EventLog<TEventDto> Log<TEventDto>(TEventDto dto, string txHash, int logIndex, long blockNumber)
        where TEventDto : class, IEventDTO, new() =>
        new(dto, new FilterLog
        {
            TransactionHash = txHash,
            LogIndex = new HexBigInteger(logIndex),
            BlockNumber = new HexBigInteger(blockNumber),
        });
}
