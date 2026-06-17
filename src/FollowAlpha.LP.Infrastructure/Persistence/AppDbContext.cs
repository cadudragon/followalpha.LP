using FollowAlpha.LP.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FollowAlpha.LP.Infrastructure.Persistence;

/// <summary>
/// The EF Core context realizing DATA-MODEL.md. Fluent configuration only (the Application entities carry
/// no ORM annotations). Fact aggregates use their natural key as a composite primary key (scoped by
/// TenantId) so idempotent insert-if-absent is a key lookup; raw on-chain integers are TEXT columns.
/// SQLite today; the same model targets the Postgres seam (no provider-specific SQL here).
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Working state (CRUD)
    public DbSet<Chain> Chains => Set<Chain>();
    public DbSet<DexProtocol> DexProtocols => Set<DexProtocol>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Pool> Pools => Set<Pool>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<WalletPositionOwnership> WalletPositionOwnerships => Set<WalletPositionOwnership>();
    public DbSet<WalletSyncCursor> WalletSyncCursors => Set<WalletSyncCursor>();

    // Facts (append-only, natural-key PK)
    public DbSet<PriceBar> PriceBars => Set<PriceBar>();
    public DbSet<PoolSnapshot> PoolSnapshots => Set<PoolSnapshot>();
    public DbSet<TickLiquiditySnapshot> TickLiquiditySnapshots => Set<TickLiquiditySnapshot>();
    public DbSet<PositionEvent> PositionEvents => Set<PositionEvent>();

    // Position projection + intent history
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<IntentRecord> IntentRecords => Set<IntentRecord>();

    // Decision records + analysis outputs (append-only)
    public DbSet<DecisionLogEntry> DecisionLogEntries => Set<DecisionLogEntry>();
    public DbSet<DecisionAnnotation> DecisionAnnotations => Set<DecisionAnnotation>();
    public DbSet<BacktestRun> BacktestRuns => Set<BacktestRun>();
    public DbSet<AuditReport> AuditReports => Set<AuditReport>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite stores DateTimeOffset as TEXT and refuses to ORDER BY it. Store it as a sortable long
        // (round-trips exactly; all timestamps are UTC per ARCHITECTURE §8). Works the same on Postgres.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // One IEntityTypeConfiguration<T> per entity under Persistence/Configurations. They realize the
        // DATA-MODEL.md §3 relationships as enforced foreign keys (no navigation properties — entities stay
        // POCOs; FK-by-scalar, OnDelete Restrict since these aggregates are append-only/rebuildable).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
