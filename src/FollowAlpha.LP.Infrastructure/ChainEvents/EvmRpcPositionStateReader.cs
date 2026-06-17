using FollowAlpha.LP.Application.ChainEvents;
using FollowAlpha.LP.Application.Protocols;

namespace FollowAlpha.LP.Infrastructure.ChainEvents;

/// <summary>
/// <see cref="IPositionStateReader"/> over EVM RPC (ARCHITECTURE.md §6). Resolves the per-chain NPM and
/// factory addresses from the <see cref="IDexProtocolRegistry"/> descriptor, then delegates the read-only
/// contract calls (<c>positions</c>, <c>getPool</c>, <c>decimals</c>) to the injected <see cref="IEvmRpc"/>.
/// The real Nethereum encode/decode is exercised offline by the transport-level RecordedRpcClient test.
/// </summary>
public sealed class EvmRpcPositionStateReader(IDexProtocolRegistry registry, IEvmRpc rpc) : IPositionStateReader
{
    public Task<ChainPositionState> GetPositionStateAsync(string chainId, string tokenId, CancellationToken cancellationToken = default) =>
        rpc.GetPositionsAsync(chainId, registry.GetByChain(chainId).PositionManagerAddress, tokenId, cancellationToken);

    public Task<string> GetPoolAddressAsync(string chainId, string token0, string token1, int feeTier, CancellationToken cancellationToken = default) =>
        rpc.GetPoolAddressAsync(chainId, registry.GetByChain(chainId).FactoryAddress, token0, token1, feeTier, cancellationToken);

    public Task<int> GetTokenDecimalsAsync(string chainId, string tokenAddress, CancellationToken cancellationToken = default) =>
        rpc.GetTokenDecimalsAsync(chainId, tokenAddress, cancellationToken);
}
