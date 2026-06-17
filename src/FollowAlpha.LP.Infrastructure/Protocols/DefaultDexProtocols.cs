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
    // Native Uniswap v3 schema subgraph ids: The Graph Explorer, recorded/revalidated 2026-06-16
    // against the decentralized gateway. Do not replace these with Messari schema ids: the adapter
    // queries native entities such as pools, poolDayData and ticks.
    // PositionManager addresses are consumed by the Phase 2.3 event reader — revalidate there.
    public static IReadOnlyList<DexProtocolDescriptor> UniswapV3 { get; } =
    [
        new DexProtocolDescriptor(
            ChainId: "arbitrum",
            DexId: "uniswap-v3",
            SubgraphId: "FbCGRftH4a3yZugY7TnbYgPJVEv2LvMT6oF1fxPe9aJM",
            SubgraphDeploymentId: "QmZ5uwhnwsJXAQGYEF8qKPQ85iVhYAcVZcZAPfrF7ZNb9z",
            PositionManagerAddress: "0xC36442b4a4522E871399CD717aBDD847Ab11FE88",
            // Uniswap v3 canonical factory (consumed by the 2.4 position-state reader's getPool).
            FactoryAddress: "0x1F98431c8aD98523631AE4a59f267346ea31F984",
            FeeTiers: [100, 500, 3000, 10000],
            Source: "The Graph Explorer",
            RecordedOnUtc: new DateOnly(2026, 6, 16)),
        new DexProtocolDescriptor(
            ChainId: "base",
            DexId: "uniswap-v3",
            SubgraphId: "96eJ9Go8gFjySRGnndG7EYxThaiwVDV8BYPp1TMDcoYh",
            SubgraphDeploymentId: "QmPb76mWQkpwbVgCrCwtFkXCy81o929RNNqbhW1pLpXACe",
            PositionManagerAddress: "0x03a520b32C04BF3bEEf7BEb72E919cf822Ed34f1",
            // Uniswap v3 factory on Base (distinct from the canonical address).
            FactoryAddress: "0x33128a8fC17869897dcE68Ed026d694621f6FDfD",
            FeeTiers: [100, 500, 3000, 10000],
            Source: "The Graph Explorer",
            RecordedOnUtc: new DateOnly(2026, 6, 16)),
    ];
}
