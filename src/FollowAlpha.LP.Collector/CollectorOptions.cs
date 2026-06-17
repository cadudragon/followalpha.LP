using FollowAlpha.LP.Application.Pools;

namespace FollowAlpha.LP.Collector;

/// <summary>
/// Collector configuration (bound from the <c>Collector</c> config section + env overrides). Secrets are
/// never here — they come from env/user-secrets (GRAPH_API_KEY, RPC_URL_*, ALCHEMY_API_KEY). The watchlist
/// and wallets drive seeding and the scheduled jobs.
/// </summary>
public sealed class CollectorOptions
{
    public const string SectionName = "Collector";

    /// <summary>SQLite file path (overridden by <c>LP_DB_PATH</c> when set).</summary>
    public string DbPath { get; set; } = "./data/followalpha-lp.db";

    /// <summary>Cron for the pool/tick snapshot job (default hourly; NFR A2).</summary>
    public string PoolSnapshotCron { get; set; } = "0 * * * *";

    /// <summary>Cron for the wallet event-sync job (default every 15 minutes).</summary>
    public string WalletSyncCron { get; set; } = "*/15 * * * *";

    /// <summary>Cron for the price-series refresh job (default daily, just after midnight UTC).</summary>
    public string PriceRefreshCron { get; set; } = "5 0 * * *";

    /// <summary>How many days of daily USD bars to request per price refresh (window re-fetched idempotently).</summary>
    public int PriceRefreshDays { get; set; } = 90;

    /// <summary>Freshness target (seconds) for pool snapshots; <c>/health</c> flags age beyond 2× this (NFR A3).</summary>
    public int PoolSnapshotFreshnessSeconds { get; set; } = 3600;

    /// <summary>Run each job once at startup (so a fresh deploy fills data without waiting for the first tick).</summary>
    public bool RunJobsOnStartup { get; set; } = true;

    /// <summary>Lower bound block for wallet sync (0 = from genesis; the head is queried at run time).</summary>
    public long WalletSyncFromBlock { get; set; }

    /// <summary>Blocks to rewind the cursor resume point by, to absorb shallow reorgs (L2 finalizes fast).</summary>
    public int WalletSyncReorgBuffer { get; set; } = 64;

    /// <summary>Max block span per <c>eth_getLogs</c> request (chunking; see <c>EvmRpcOptions.MaxBlockSpan</c>).</summary>
    public long RpcMaxBlockSpan { get; set; } = 50_000;

    /// <summary>Path to the audit wallets file (<c>config/wallets.json</c> by convention).</summary>
    public string WalletsPath { get; set; } = "config/wallets.json";

    /// <summary>Watchlist pools to snapshot (and to seed the reference graph).</summary>
    public List<WatchlistPool> Watchlist { get; set; } = [];
}

/// <summary>A watchlist pool descriptor (config), enough to seed the pool + its assets and to snapshot it.</summary>
public sealed class WatchlistPool
{
    public required string ChainId { get; set; }
    public required string Address { get; set; }
    public int FeeTier { get; set; }
    public int TickSpacing { get; set; }
    public required WatchlistAsset Token0 { get; set; }
    public required WatchlistAsset Token1 { get; set; }

    public string PoolId => PoolIdentity.For(ChainId, Address);
}

/// <summary>
/// An asset descriptor used to seed chain-aware <c>Asset</c> rows referenced by watchlist pools. The
/// <c>Asset.Id</c> is derived as <c>{ChainId}:{Address}</c> (see <c>AssetIdentity</c>), so the same symbol
/// on different chains does not collide.
/// </summary>
public sealed class WatchlistAsset
{
    /// <summary>The token contract address on the pool's chain.</summary>
    public required string Address { get; set; }
    public required string Symbol { get; set; }
    public int Decimals { get; set; }
}
