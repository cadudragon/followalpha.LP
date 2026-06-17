namespace FollowAlpha.LP.Application.Persistence;

// Working-state aggregates (DATA-MODEL.md §2 — normal CRUD). Plain POCOs: EF (Infrastructure) maps them
// via Fluent configuration; Application stays free of any ORM annotations. TenantId on every aggregate.

/// <summary>A chain descriptor (e.g. <c>arbitrum</c>, <c>base</c>).</summary>
public sealed class Chain
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string RpcEnvVarName { get; set; }
    public bool Enabled { get; set; }
    public string TenantId { get; set; } = Tenancy.DefaultTenantId;
}

/// <summary>A DEX protocol on a chain (e.g. <c>uniswap-v3</c>) — see <c>IDexProtocolRegistry</c>.</summary>
public sealed class DexProtocol
{
    public required string Id { get; set; }
    public required string ChainId { get; set; }
    public required string SubgraphId { get; set; }
    public required string PositionManagerAddress { get; set; }
    /// <summary>JSON array of supported fee tiers.</summary>
    public required string FeeTiers { get; set; }
    public bool Enabled { get; set; }
    public string TenantId { get; set; } = Tenancy.DefaultTenantId;
}

/// <summary>
/// A tradable asset. <see cref="Id"/> is chain-aware (<c>{ChainId}:{Address}</c>, see
/// <c>AssetIdentity</c>): the same symbol has different contract addresses per chain, so a symbol-only id
/// would collide across Arbitrum/Base. The watchlist is the set flagged <see cref="InWatchlist"/>.
/// </summary>
public sealed class Asset
{
    public required string Id { get; set; }
    public required string ChainId { get; set; }
    /// <summary>The token contract address on <see cref="ChainId"/> (lower-cased).</summary>
    public required string Address { get; set; }
    public required string Symbol { get; set; }
    public int Decimals { get; set; }
    public string? ChainlinkFeedAddress { get; set; }
    public bool InWatchlist { get; set; }
    public string TenantId { get; set; } = Tenancy.DefaultTenantId;
}

/// <summary>A concentrated-liquidity pool (id = chain + pool address).</summary>
public sealed class Pool
{
    public required string Id { get; set; }
    public required string ChainId { get; set; }
    public required string DexProtocolId { get; set; }
    public required string Token0AssetId { get; set; }
    public required string Token1AssetId { get; set; }
    public int FeeTier { get; set; }
    public int TickSpacing { get; set; }
    public required string Address { get; set; }
    public bool InWatchlist { get; set; }
    public string TenantId { get; set; } = Tenancy.DefaultTenantId;
}

/// <summary>An audit-target wallet (see <c>config/wallets.json</c>).</summary>
public sealed class Wallet
{
    public required string Id { get; set; }
    public required string Address { get; set; }
    public required string Label { get; set; }
    /// <summary>JSON array of chain ids this wallet is tracked on.</summary>
    public required string Chains { get; set; }
    public string TenantId { get; set; } = Tenancy.DefaultTenantId;
}

/// <summary>An alert rule (UC-07).</summary>
public sealed class AlertRule
{
    public Guid Id { get; set; }
    public required string Type { get; set; }
    public required string TargetRef { get; set; }
    /// <summary>JSON parameters for the rule.</summary>
    public required string Params { get; set; }
    public bool Enabled { get; set; }
    public string? NotificationChannelId { get; set; }
    public string TenantId { get; set; } = Tenancy.DefaultTenantId;
}

/// <summary>A key/value application setting.</summary>
public sealed class AppSetting
{
    public required string Key { get; set; }
    public required string Value { get; set; }
    public string TenantId { get; set; } = Tenancy.DefaultTenantId;
}

/// <summary>
/// One ownership interval of a position NFT for a wallet, built incrementally from NPM ERC-721
/// <c>Transfer</c> logs (DATA-MODEL.md §2). A <see cref="PositionEvent"/> is attributed to the wallet only
/// when its <c>(block, logIndex)</c> falls inside an open interval, so owner-at-time attribution cannot
/// contaminate the append-only audit truth. <see cref="Seq"/> orders re-acquisitions of the same tokenId.
/// Working state (rebuildable from chain), keyed (TenantId, ChainId, WalletId, TokenId, Seq).
/// </summary>
public sealed class WalletPositionOwnership
{
    public string TenantId { get; set; } = Tenancy.DefaultTenantId;
    public required string ChainId { get; set; }
    public required string WalletId { get; set; }
    public required string TokenId { get; set; }
    /// <summary>0-based index of this acquisition among repeated acquisitions of the same tokenId.</summary>
    public int Seq { get; set; }
    public long AcquiredBlock { get; set; }
    public int AcquiredLogIndex { get; set; }
    /// <summary>Block of the Transfer-out that closed the interval; null while still owned.</summary>
    public long? ReleasedBlock { get; set; }
    public int? ReleasedLogIndex { get; set; }
}

/// <summary>
/// The high-water mark of the wallet event-sync per (chain, wallet), so the scheduled job resumes
/// incrementally instead of rescanning from genesis (DATA-MODEL.md §2). Advanced only after a window
/// syncs successfully. Working state, keyed (TenantId, ChainId, WalletId).
/// </summary>
public sealed class WalletSyncCursor
{
    public string TenantId { get; set; } = Tenancy.DefaultTenantId;
    public required string ChainId { get; set; }
    public required string WalletId { get; set; }
    public long LastScannedBlock { get; set; }
}
