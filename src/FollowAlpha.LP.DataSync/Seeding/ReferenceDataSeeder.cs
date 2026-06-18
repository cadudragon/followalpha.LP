using System.Text.Json;
using FollowAlpha.LP.Application.Persistence;
using FollowAlpha.LP.Application.Pools;
using FollowAlpha.LP.Application.Protocols;
using FollowAlpha.LP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FollowAlpha.LP.DataSync.Seeding;

/// <summary>
/// Seeds the working-state reference graph the fact tables depend on via foreign keys (DATA-MODEL.md §3):
/// chains and DEX protocols (from the registry descriptors), the watchlist pools and their assets (from
/// config), and the audit wallets (from <c>config/wallets.json</c>). Idempotent insert-if-absent on the
/// primary keys — safe to run on every startup. Working-state CRUD ports are still deferred
/// (OPEN-DECISIONS.md); the DataSync seeds directly through the context at its composition root.
/// </summary>
public static class ReferenceDataSeeder
{
    public static async Task SeedAsync(
        AppDbContext db,
        IDexProtocolRegistry registry,
        DataSyncOptions options,
        WalletsFile wallets,
        CancellationToken cancellationToken = default)
    {
        foreach (var descriptor in registry.All)
        {
            await EnsureChainAsync(db, descriptor.ChainId, cancellationToken);
            await EnsureDexProtocolAsync(db, descriptor, cancellationToken);
        }

        foreach (var pool in options.Watchlist)
        {
            await EnsureAssetAsync(db, pool.ChainId, pool.Token0, cancellationToken);
            await EnsureAssetAsync(db, pool.ChainId, pool.Token1, cancellationToken);
            await EnsurePoolAsync(db, pool, registry.GetByChain(pool.ChainId), cancellationToken);
        }

        foreach (var wallet in wallets.Wallets)
        {
            await EnsureWalletAsync(db, wallet, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The per-chain DexProtocol id (DexId is shared across chains, so the pk is scoped by chain).</summary>
    public static string DexProtocolId(DexProtocolDescriptor descriptor) => $"{descriptor.DexId}:{descriptor.ChainId}";

    private static async Task EnsureChainAsync(AppDbContext db, string chainId, CancellationToken ct)
    {
        if (await db.Chains.AnyAsync(c => c.Id == chainId, ct))
        {
            return;
        }

        db.Chains.Add(new Chain
        {
            Id = chainId,
            Name = chainId,
            RpcEnvVarName = $"RPC_URL_{chainId.ToUpperInvariant()}",
            Enabled = true,
        });
    }

    private static async Task EnsureDexProtocolAsync(AppDbContext db, DexProtocolDescriptor descriptor, CancellationToken ct)
    {
        var id = DexProtocolId(descriptor);
        if (await db.DexProtocols.AnyAsync(d => d.Id == id, ct))
        {
            return;
        }

        db.DexProtocols.Add(new DexProtocol
        {
            Id = id,
            ChainId = descriptor.ChainId,
            SubgraphId = descriptor.SubgraphId,
            PositionManagerAddress = descriptor.PositionManagerAddress,
            FeeTiers = JsonSerializer.Serialize(descriptor.FeeTiers),
            Enabled = true,
        });
    }

    private static async Task EnsureAssetAsync(AppDbContext db, string chainId, WatchlistAsset asset, CancellationToken ct)
    {
        var id = AssetIdentity.For(chainId, asset.Address);
        if (await db.Assets.AnyAsync(a => a.Id == id, ct))
        {
            return;
        }

        db.Assets.Add(new Asset
        {
            Id = id,
            ChainId = chainId,
            Address = asset.Address.ToLowerInvariant(),
            Symbol = asset.Symbol,
            Decimals = asset.Decimals,
            InWatchlist = true,
        });
    }

    private static async Task EnsurePoolAsync(AppDbContext db, WatchlistPool pool, DexProtocolDescriptor descriptor, CancellationToken ct)
    {
        if (await db.Pools.AnyAsync(p => p.Id == pool.PoolId, ct))
        {
            return;
        }

        db.Pools.Add(new Pool
        {
            Id = pool.PoolId,
            ChainId = pool.ChainId,
            DexProtocolId = DexProtocolId(descriptor),
            Token0AssetId = AssetIdentity.For(pool.ChainId, pool.Token0.Address),
            Token1AssetId = AssetIdentity.For(pool.ChainId, pool.Token1.Address),
            FeeTier = pool.FeeTier,
            TickSpacing = pool.TickSpacing,
            Address = pool.Address,
            InWatchlist = true,
        });
    }

    private static async Task EnsureWalletAsync(AppDbContext db, WalletEntry wallet, CancellationToken ct)
    {
        var id = wallet.Address.ToLowerInvariant();
        if (await db.Wallets.AnyAsync(w => w.Id == id, ct))
        {
            return;
        }

        db.Wallets.Add(new Wallet
        {
            Id = id,
            Address = wallet.Address,
            Label = wallet.Label,
            Chains = JsonSerializer.Serialize(wallet.Chains),
        });
    }
}
