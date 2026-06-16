using FollowAlpha.LP.Application.Protocols;

namespace FollowAlpha.LP.Infrastructure.Protocols;

/// <summary>
/// Default Uniswap v3 descriptors for the day-1 chains. These are <b>revalidable configuration</b>: the
/// composition root may override them from <c>appsettings.json</c> (ARCHITECTURE.md §9). Subgraph ids
/// live on the decentralized network and can change — source and date are recorded for revalidation, and
/// the descriptor supports pinning a deployment id later without code changes.
/// </summary>
public static class DefaultDexProtocols
{
    // Subgraph ids: The Graph Explorer, recorded 2026-06-16 (Uniswap v3, Arbitrum + Base).
    // PositionManager addresses are consumed by the Phase 2.3 event reader — revalidate there.
    public static IReadOnlyList<DexProtocolDescriptor> UniswapV3 { get; } =
    [
        new DexProtocolDescriptor(
            ChainId: "arbitrum",
            DexId: "uniswap-v3",
            SubgraphId: "FQ6JYszEKApsBpAmiHesRsd9Ygc6mzmpNRANeVQFYoVX",
            SubgraphDeploymentId: null,
            PositionManagerAddress: "0xC36442b4a4522E871399CD717aBDD847Ab11FE88",
            FeeTiers: [100, 500, 3000, 10000],
            Source: "The Graph Explorer",
            RecordedOnUtc: new DateOnly(2026, 6, 16)),
        new DexProtocolDescriptor(
            ChainId: "base",
            DexId: "uniswap-v3",
            SubgraphId: "FUbEPQw1oMghy39fwWBFY5fE6MXPXZQtjncQy2cXdrNS",
            SubgraphDeploymentId: null,
            PositionManagerAddress: "0x03a520b32C04BF3bEEf7BEb72E919cf822Ed34f1",
            FeeTiers: [100, 500, 3000, 10000],
            Source: "The Graph Explorer",
            RecordedOnUtc: new DateOnly(2026, 6, 16)),
    ];
}
