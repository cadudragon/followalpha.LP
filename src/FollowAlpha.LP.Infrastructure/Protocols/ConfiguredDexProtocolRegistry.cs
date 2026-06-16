using FollowAlpha.LP.Application.Protocols;

namespace FollowAlpha.LP.Infrastructure.Protocols;

/// <summary>
/// An <see cref="IDexProtocolRegistry"/> backed by a fixed set of descriptors (bound from config at the
/// composition root, or <see cref="DefaultDexProtocols"/> as defaults). One descriptor per chain in v1.
/// </summary>
public sealed class ConfiguredDexProtocolRegistry : IDexProtocolRegistry
{
    private readonly Dictionary<string, DexProtocolDescriptor> _byChain;

    public ConfiguredDexProtocolRegistry(IEnumerable<DexProtocolDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        _byChain = descriptors.ToDictionary(d => d.ChainId, StringComparer.OrdinalIgnoreCase);
        All = [.. _byChain.Values];
    }

    public IReadOnlyList<DexProtocolDescriptor> All { get; }

    public DexProtocolDescriptor GetByChain(string chainId) =>
        FindByChain(chainId) ?? throw new KeyNotFoundException($"No DEX descriptor configured for chain '{chainId}'.");

    public DexProtocolDescriptor? FindByChain(string chainId) =>
        _byChain.TryGetValue(chainId, out var descriptor) ? descriptor : null;
}
