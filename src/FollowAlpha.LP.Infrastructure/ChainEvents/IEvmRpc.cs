using System.Numerics;
using FollowAlpha.LP.Application.ChainEvents;
using Nethereum.RPC.Eth.DTOs;

namespace FollowAlpha.LP.Infrastructure.ChainEvents;

/// <summary>
/// The thin RPC seam the event reader and position-state reader depend on: <b>raw</b> event logs (filtered
/// by event signature and indexed topic), gas from a receipt, a block timestamp, and read-only contract
/// calls (NPM <c>positions</c>, factory <c>getPool</c>, ERC-20 <c>decimals</c>) — per chain. Production uses
/// Nethereum (<see cref="NethereumEvmRpc"/>); unit tests provide a fake returning ABI-correct recorded logs,
/// and a transport-level <c>RecordedRpcClient</c> replays real JSON-RPC captures through the real
/// <see cref="NethereumEvmRpc"/>. Public only so the host can wire it and tests can fake it.
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

    /// <summary>
    /// Raw ERC-721 <c>Transfer</c> logs emitted by <paramref name="address"/> in <c>[fromBlock, toBlock]</c>,
    /// optionally filtered to sender <paramref name="fromAddress"/> (indexed topic1) and/or recipient
    /// <paramref name="toAddress"/> (indexed topic2) — a null filter is a wildcard. The tokenId is indexed
    /// topic3. (A single <c>eth_getLogs</c> ANDs topics, so "from OR to" is two calls by the caller.)
    /// </summary>
    Task<IReadOnlyList<FilterLog>> GetTransferLogsAsync(
        string chainId,
        string address,
        string? fromAddress,
        string? toAddress,
        long fromBlock,
        long toBlock,
        CancellationToken cancellationToken);

    Task<GasInfo> GetGasAsync(string chainId, string txHash, CancellationToken cancellationToken);

    Task<DateTimeOffset> GetBlockTimestampAsync(string chainId, long blockNumber, CancellationToken cancellationToken);

    /// <summary>The latest block number on the chain.</summary>
    Task<long> GetLatestBlockNumberAsync(string chainId, CancellationToken cancellationToken);

    /// <summary>NPM <c>positions(tokenId)</c>: range, token pair, and fee tier.</summary>
    Task<ChainPositionState> GetPositionsAsync(string chainId, string positionManagerAddress, string tokenId, CancellationToken cancellationToken);

    /// <summary>Factory <c>getPool(token0, token1, fee)</c>: the pool address (lower-cased).</summary>
    Task<string> GetPoolAddressAsync(string chainId, string factoryAddress, string token0, string token1, int feeTier, CancellationToken cancellationToken);

    /// <summary>ERC-20 <c>decimals()</c>.</summary>
    Task<int> GetTokenDecimalsAsync(string chainId, string tokenAddress, CancellationToken cancellationToken);
}

/// <summary>
/// Gas accounting from a transaction receipt (native units). <see cref="EffectiveGasPriceWei"/> is null
/// when the receipt does not report it — never silently zero, so a false-zero gas cannot contaminate an audit.
/// </summary>
public readonly record struct GasInfo(BigInteger GasUsed, BigInteger? EffectiveGasPriceWei)
{
    public BigInteger? NativeCostWei => EffectiveGasPriceWei is { } price ? GasUsed * price : null;
}
