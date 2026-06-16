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
        // Foreign keys realize the DATA-MODEL.md §3 relationships and are enforced by the database
        // (SQLite has foreign_keys ON via Microsoft.Data.Sqlite). No navigation properties — the entities
        // stay plain POCOs; relationships are configured by FK scalar only. Restrict everywhere: these
        // aggregates are append-only/rebuildable and are never cascade-deleted.

        // Working state
        modelBuilder.Entity<Chain>().HasKey(e => e.Id);
        modelBuilder.Entity<DexProtocol>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasOne<Chain>().WithMany().HasForeignKey(e => e.ChainId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Asset>().HasKey(e => e.Id);
        modelBuilder.Entity<Pool>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasOne<Chain>().WithMany().HasForeignKey(e => e.ChainId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<DexProtocol>().WithMany().HasForeignKey(e => e.DexProtocolId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Asset>().WithMany().HasForeignKey(e => e.Token0AssetId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Asset>().WithMany().HasForeignKey(e => e.Token1AssetId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Wallet>().HasKey(e => e.Id);
        modelBuilder.Entity<AlertRule>().HasKey(e => e.Id); // TargetRef is polymorphic (Type+ref) — no FK.
        modelBuilder.Entity<AppSetting>().HasKey(e => e.Key);

        // Facts: composite natural key = primary key (scoped by TenantId).
        modelBuilder.Entity<PriceBar>(b =>
        {
            b.HasKey(e => new { e.TenantId, e.AssetId, e.Resolution, e.OpenTimeUtc });
            b.HasOne<Asset>().WithMany().HasForeignKey(e => e.AssetId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PoolSnapshot>(b =>
        {
            b.HasKey(e => new { e.TenantId, e.PoolId, e.AsOfUtc });
            b.HasOne<Pool>().WithMany().HasForeignKey(e => e.PoolId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<TickLiquiditySnapshot>(b =>
        {
            b.HasKey(e => new { e.TenantId, e.PoolId, e.AsOfUtc, e.Tick });
            b.HasOne<Pool>().WithMany().HasForeignKey(e => e.PoolId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PositionEvent>(b =>
        {
            b.HasKey(e => new { e.TenantId, e.ChainId, e.TxHash, e.LogIndex });
            b.HasOne<Wallet>().WithMany().HasForeignKey(e => e.WalletId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Pool>().WithMany().HasForeignKey(e => e.PoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // Position projection + intent history
        modelBuilder.Entity<Position>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasOne<Wallet>().WithMany().HasForeignKey(e => e.WalletId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Pool>().WithMany().HasForeignKey(e => e.PoolId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<IntentRecord>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.TenantId, e.PositionId });
            b.HasOne<Position>().WithMany().HasForeignKey(e => e.PositionId).OnDelete(DeleteBehavior.Restrict);
            // Self-FK: a reclassification supersedes a prior record in the same chain.
            b.HasOne<IntentRecord>().WithMany().HasForeignKey(e => e.SupersedesIntentRecordId).OnDelete(DeleteBehavior.Restrict);
        });

        // Decision records + analysis outputs
        modelBuilder.Entity<DecisionLogEntry>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.TenantId, e.PoolId });
            b.HasOne<Pool>().WithMany().HasForeignKey(e => e.PoolId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<DecisionAnnotation>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.DecisionLogEntryId);
            b.HasOne<DecisionLogEntry>().WithMany().HasForeignKey(e => e.DecisionLogEntryId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<BacktestRun>().HasKey(e => e.Id);
        modelBuilder.Entity<AuditReport>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasOne<Wallet>().WithMany().HasForeignKey(e => e.WalletId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
