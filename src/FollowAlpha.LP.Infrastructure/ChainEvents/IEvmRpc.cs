using System.Numerics;
using Nethereum.RPC.Eth.DTOs;

namespace FollowAlpha.LP.Infrastructure.ChainEvents;

/// <summary>
/// The thin RPC seam the event reader depends on: <b>raw</b> event logs (filtered by event signature and
/// indexed tokenId), gas from a receipt, and a block timestamp — per chain. Production uses Nethereum
/// (<see cref="NethereumEvmRpc"/>); tests provide a fake returning ABI-correct recorded logs, so the
/// reader's log→DTO decode and mapping run offline (no live network, no RPC key). Public only so the host
/// can wire it and tests can fake it.
/// </summary>
public interface IEvmRpc
{
    /// <summary>
    /// Raw logs for <paramref name="eventSignatureTopic"/> (topic0) emitted by <paramref name="address"/>
    /// in <c>[fromBlock, toBlock]</c>, filtered to the given indexed <paramref name="tokenIds"/> (topic1).
    /// </summary>
    Task<IReadOnlyList<FilterLog>> GetLogsAsync(
        string chainId,
        string address,
        string eventSignatureTopic,
        IReadOnlyCollection<string> tokenIds,
        long fromBlock,
        long toBlock,
        CancellationToken cancellationToken);

    Task<GasInfo> GetGasAsync(string chainId, string txHash, CancellationToken cancellationToken);

    Task<DateTimeOffset> GetBlockTimestampAsync(string chainId, long blockNumber, CancellationToken cancellationToken);
}

/// <summary>
/// Gas accounting from a transaction receipt (native units). <see cref="EffectiveGasPriceWei"/> is null
/// when the receipt does not report it — never silently zero, so a false-zero gas cannot contaminate an audit.
/// </summary>
public readonly record struct GasInfo(BigInteger GasUsed, BigInteger? EffectiveGasPriceWei)
{
    public BigInteger? NativeCostWei => EffectiveGasPriceWei is { } price ? GasUsed * price : null;
}
