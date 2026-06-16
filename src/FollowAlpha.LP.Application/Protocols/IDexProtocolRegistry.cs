namespace FollowAlpha.LP.Application.Protocols;

/// <summary>
/// Resolves <see cref="DexProtocolDescriptor"/>s (ARCHITECTURE.md §5). v1 has one DEX (Uniswap v3) per
/// chain, so lookup is by chain id; multi-DEX-per-chain would extend this without touching callers.
/// </summary>
public interface IDexProtocolRegistry
{
    /// <summary>All configured descriptors.</summary>
    IReadOnlyList<DexProtocolDescriptor> All { get; }

    /// <summary>The descriptor for a chain.</summary>
    /// <exception cref="KeyNotFoundException">No descriptor is configured for <paramref name="chainId"/>.</exception>
    DexProtocolDescriptor GetByChain(string chainId);

    /// <summary>The descriptor for a chain, or null if none is configured.</summary>
    DexProtocolDescriptor? FindByChain(string chainId);
}
