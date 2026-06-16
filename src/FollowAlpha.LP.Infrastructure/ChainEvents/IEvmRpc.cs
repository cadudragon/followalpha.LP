using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace FollowAlpha.LP.Infrastructure.ChainEvents;

/// <summary>
/// The thin RPC seam the event reader depends on: decoded event logs, gas from a receipt, and a block
/// timestamp — per chain. Production uses Nethereum (<see cref="NethereumEvmRpc"/>); tests provide a fake
/// returning recorded data, so the reader is testable offline with no live network and no RPC key.
/// (An infrastructure-internal seam, public only so the host can wire it and tests can fake it.)
/// </summary>
public interface IEvmRpc
{
    Task<IReadOnlyList<EventLog<TEventDto>>> GetEventsAsync<TEventDto>(
        string chainId, string contractAddress, long fromBlock, long toBlock, CancellationToken cancellationToken)
        where TEventDto : class, IEventDTO, new();

    Task<GasInfo> GetGasAsync(string chainId, string txHash, CancellationToken cancellationToken);

    Task<DateTimeOffset> GetBlockTimestampAsync(string chainId, long blockNumber, CancellationToken cancellationToken);
}

/// <summary>Gas accounting from a transaction receipt (native units).</summary>
public readonly record struct GasInfo(BigInteger GasUsed, BigInteger EffectiveGasPriceWei)
{
    public BigInteger NativeCostWei => GasUsed * EffectiveGasPriceWei;
}
