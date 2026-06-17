using FollowAlpha.LP.Application;
using FollowAlpha.LP.Application.Abstractions;
using FollowAlpha.LP.Application.ChainEvents;
using FollowAlpha.LP.Application.Collection;
using FollowAlpha.LP.Application.Persistence;
using FollowAlpha.LP.Application.Pools;
using FollowAlpha.LP.Application.Prices;
using FollowAlpha.LP.Application.Protocols;
using FollowAlpha.LP.Collector;
using FollowAlpha.LP.Collector.Jobs;
using FollowAlpha.LP.Collector.Seeding;
using FollowAlpha.LP.Infrastructure.ChainEvents;
using FollowAlpha.LP.Infrastructure.Persistence;
using FollowAlpha.LP.Infrastructure.Protocols;
using FollowAlpha.LP.Infrastructure.TheGraph;
using System.Globalization;
using FollowAlpha.LP.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

// Options (Collector section + env). Secrets are read from env/user-secrets, never bound from the repo.
builder.Services.Configure<CollectorOptions>(builder.Configuration.GetSection(CollectorOptions.SectionName));
var options = builder.Configuration.GetSection(CollectorOptions.SectionName).Get<CollectorOptions>() ?? new CollectorOptions();
var dbPath = builder.Configuration["LP_DB_PATH"] ?? options.DbPath;

// Persistence — SQLite with foreign keys enforced at runtime (OPEN-DECISIONS operational requirement).
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dbPath))!);
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={dbPath};Foreign Keys=True"));
builder.Services.AddScoped<ISnapshotStore, EfSnapshotStore>();
builder.Services.AddScoped<IPositionEventStore, EfPositionEventStore>();
builder.Services.AddScoped<IPriceStore, EfPriceStore>();
builder.Services.AddScoped<IWalletOwnershipStore, EfWalletOwnershipStore>();
builder.Services.AddScoped<IWalletSyncCursorStore, EfWalletSyncCursorStore>();

// Shared singletons.
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IDexProtocolRegistry>(new ConfiguredDexProtocolRegistry(DefaultDexProtocols.UniswapV3));
builder.Services.AddSingleton<CollectorHealth>();

// The Graph data sources (typed HttpClients + standard Polly resilience at the composition root).
builder.Services.AddSingleton(new TheGraphGatewayOptions { ApiKey = builder.Configuration["GRAPH_API_KEY"] ?? string.Empty });
builder.Services.AddHttpClient<IPoolDataSource, TheGraphPoolDataSource>().AddStandardResilienceHandler();
builder.Services.AddHttpClient<IPriceSeriesSource, TheGraphPriceSeriesSource>().AddStandardResilienceHandler();

// EVM RPC (event reader + position-state reader). URLs from env (RPC_URL_* or Alchemy), never the repo.
var rpcOptions = new EvmRpcOptions { RpcUrls = BuildRpcUrls(builder.Configuration), MaxBlockSpan = options.RpcMaxBlockSpan };
builder.Services.AddSingleton(rpcOptions);
builder.Services.AddSingleton<IEvmRpc>(_ => NethereumEvmRpc.FromOptions(rpcOptions));
builder.Services.AddSingleton<IChainEventReader, EvmRpcEventReader>();
builder.Services.AddSingleton<IPositionStateReader, EvmRpcPositionStateReader>();

// Ingestion use cases (scoped — they use the EF context/stores).
builder.Services.AddScoped<IngestPoolSnapshots>();
builder.Services.AddScoped<SyncWalletPositionEvents>();
builder.Services.AddScoped<IngestPriceSeries>();

// Scheduled jobs.
builder.Services.AddHostedService<PoolSnapshotJob>();
builder.Services.AddHostedService<WalletSyncJob>();
builder.Services.AddHostedService<PriceRefreshJob>();

var app = builder.Build();

// Migrate + seed the reference graph the fact FKs depend on.
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    var wallets = WalletsFile.LoadOrEmpty(options.WalletsPath, app.Environment.ContentRootPath);
    await ReferenceDataSeeder.SeedAsync(db, sp.GetRequiredService<IDexProtocolRegistry>(), options, wallets);
    Log.Information("Seeded reference graph: {Pools} watchlist pools, {Wallets} wallets.", options.Watchlist.Count, wallets.Wallets.Count);
}

// Health: per-pool snapshot freshness + last job runs (NFR A3/O2). 503 when any pool is stale.
app.MapGet("/health", async (ISnapshotStore snapshots, IOptions<CollectorOptions> opts, IClock clock, CollectorHealth health) =>
{
    var now = clock.UtcNow;
    var staleAfter = TimeSpan.FromSeconds(opts.Value.PoolSnapshotFreshnessSeconds * 2);
    var pools = new List<object>();
    var anyStale = false;

    foreach (var pool in opts.Value.Watchlist)
    {
        var latest = await snapshots.GetLatestPoolSnapshotAsync(Tenancy.DefaultTenantId, pool.PoolId);
        DateTimeOffset? asOf = latest?.AsOfUtc;
        var stale = asOf is null || now - asOf.Value > staleAfter;
        anyStale |= stale;
        pools.Add(new
        {
            poolId = pool.PoolId,
            lastSnapshotUtc = asOf,
            ageSeconds = asOf is { } a ? (now - a).TotalSeconds : (double?)null,
            stale,
        });
    }

    var body = new
    {
        status = anyStale ? "Degraded" : "Healthy",
        timeUtc = now,
        jobs = new { poolSnapshot = health.GetLastRun("pool-snapshot"), walletSync = health.GetLastRun("wallet-sync") },
        pools,
    };

    return Results.Json(body, statusCode: anyStale ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status200OK);
});

app.Run();

static Dictionary<string, string> BuildRpcUrls(IConfiguration config)
{
    var urls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var alchemy = config["ALCHEMY_API_KEY"];
    Add("arbitrum", "RPC_URL_ARBITRUM", "arb-mainnet");
    Add("base", "RPC_URL_BASE", "base-mainnet");
    return urls;

    void Add(string chainId, string envVar, string alchemySubdomain)
    {
        var url = config[envVar];
        if (string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(alchemy))
        {
            url = $"https://{alchemySubdomain}.g.alchemy.com/v2/{alchemy}";
        }

        if (!string.IsNullOrWhiteSpace(url))
        {
            urls[chainId] = url;
        }
    }
}
