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

/// <summary>A tradable asset (e.g. <c>ETH</c>). The watchlist is the set flagged <see cref="InWatchlist"/>.</summary>
public sealed class Asset
{
    public required string Id { get; set; }
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
