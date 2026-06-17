namespace FollowAlpha.LP.Application.Protocols;

/// <summary>
/// A DEX-on-chain descriptor (<c>IDexProtocolRegistry</c>): everything an adapter needs to query a
/// protocol on a chain — its subgraph and (optionally) a pinned deployment, the position-manager
/// address, and the supported fee tiers. Adding a DEX/chain is adding a descriptor, not code
/// (ARCHITECTURE.md §5/§6). <see cref="SubgraphId"/> is the v1 default; <see cref="SubgraphDeploymentId"/>
/// is preferred later for version pinning — the adapter picks whichever is set, no code change.
/// <see cref="Source"/>/<see cref="RecordedOnUtc"/> keep the (revalidable) provenance of the ids.
/// </summary>
public sealed record DexProtocolDescriptor(
    string ChainId,
    string DexId,
    string SubgraphId,
    string? SubgraphDeploymentId,
    string PositionManagerAddress,
    string FactoryAddress,
    IReadOnlyList<int> FeeTiers,
    string Source,
    DateOnly RecordedOnUtc);
