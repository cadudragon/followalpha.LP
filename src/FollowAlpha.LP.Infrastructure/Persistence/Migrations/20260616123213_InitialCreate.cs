using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FollowAlpha.LP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlertRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    TargetRef = table.Column<string>(type: "TEXT", nullable: false),
                    Params = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotificationChannelId = table.Column<string>(type: "TEXT", nullable: true),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", nullable: false),
                    Decimals = table.Column<int>(type: "INTEGER", nullable: false),
                    ChainlinkFeedAddress = table.Column<string>(type: "TEXT", nullable: true),
                    InWatchlist = table.Column<bool>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    WalletId = table.Column<string>(type: "TEXT", nullable: false),
                    ResultJson = table.Column<string>(type: "TEXT", nullable: false),
                    InputDataHash = table.Column<string>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BacktestRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    ParamsJson = table.Column<string>(type: "TEXT", nullable: false),
                    ResultJson = table.Column<string>(type: "TEXT", nullable: false),
                    DataWindowFromUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    DataWindowToUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    InputDataHash = table.Column<string>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BacktestRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Chains",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    RpcEnvVarName = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chains", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DecisionAnnotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DecisionLogEntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecisionAnnotations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DecisionLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    PoolId = table.Column<string>(type: "TEXT", nullable: false),
                    Intent = table.Column<string>(type: "TEXT", nullable: true),
                    Capital = table.Column<decimal>(type: "TEXT", nullable: false),
                    TickLower = table.Column<int>(type: "INTEGER", nullable: false),
                    TickUpper = table.Column<int>(type: "INTEGER", nullable: false),
                    InputsJson = table.Column<string>(type: "TEXT", nullable: false),
                    Verdict = table.Column<string>(type: "TEXT", nullable: true),
                    ExpectancyNet = table.Column<decimal>(type: "TEXT", nullable: false),
                    ContentHash = table.Column<string>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecisionLogEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DexProtocols",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ChainId = table.Column<string>(type: "TEXT", nullable: false),
                    SubgraphId = table.Column<string>(type: "TEXT", nullable: false),
                    PositionManagerAddress = table.Column<string>(type: "TEXT", nullable: false),
                    FeeTiers = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DexProtocols", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntentRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PositionId = table.Column<string>(type: "TEXT", nullable: false),
                    Intent = table.Column<string>(type: "TEXT", nullable: false),
                    DeclaredAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: true),
                    SupersedesIntentRecordId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntentRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pools",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ChainId = table.Column<string>(type: "TEXT", nullable: false),
                    DexProtocolId = table.Column<string>(type: "TEXT", nullable: false),
                    Token0AssetId = table.Column<string>(type: "TEXT", nullable: false),
                    Token1AssetId = table.Column<string>(type: "TEXT", nullable: false),
                    FeeTier = table.Column<int>(type: "INTEGER", nullable: false),
                    TickSpacing = table.Column<int>(type: "INTEGER", nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: false),
                    InWatchlist = table.Column<bool>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pools", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PoolSnapshots",
                columns: table => new
                {
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    PoolId = table.Column<string>(type: "TEXT", nullable: false),
                    AsOfUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    CurrentTick = table.Column<int>(type: "INTEGER", nullable: false),
                    SqrtPriceX96 = table.Column<string>(type: "TEXT", nullable: false),
                    Liquidity = table.Column<string>(type: "TEXT", nullable: false),
                    Tvl = table.Column<decimal>(type: "TEXT", nullable: false),
                    DayVolumeUsd = table.Column<decimal>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoolSnapshots", x => new { x.TenantId, x.PoolId, x.AsOfUtc });
                });

            migrationBuilder.CreateTable(
                name: "PositionEvents",
                columns: table => new
                {
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    ChainId = table.Column<string>(type: "TEXT", nullable: false),
                    TxHash = table.Column<string>(type: "TEXT", nullable: false),
                    LogIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    WalletId = table.Column<string>(type: "TEXT", nullable: false),
                    PoolId = table.Column<string>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", nullable: false),
                    TickLower = table.Column<int>(type: "INTEGER", nullable: false),
                    TickUpper = table.Column<int>(type: "INTEGER", nullable: false),
                    LiquidityDelta = table.Column<string>(type: "TEXT", nullable: false),
                    Amount0 = table.Column<decimal>(type: "TEXT", nullable: false),
                    Amount1 = table.Column<decimal>(type: "TEXT", nullable: false),
                    FeesCollected0 = table.Column<decimal>(type: "TEXT", nullable: false),
                    FeesCollected1 = table.Column<decimal>(type: "TEXT", nullable: false),
                    GasCostUsd = table.Column<decimal>(type: "TEXT", nullable: false),
                    BlockTimeUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PositionEvents", x => new { x.TenantId, x.ChainId, x.TxHash, x.LogIndex });
                });

            migrationBuilder.CreateTable(
                name: "Positions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WalletId = table.Column<string>(type: "TEXT", nullable: false),
                    PoolId = table.Column<string>(type: "TEXT", nullable: false),
                    TickLower = table.Column<int>(type: "INTEGER", nullable: false),
                    TickUpper = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    ClosedAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PriceBars",
                columns: table => new
                {
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    AssetId = table.Column<string>(type: "TEXT", nullable: false),
                    Resolution = table.Column<string>(type: "TEXT", nullable: false),
                    OpenTimeUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    Open = table.Column<decimal>(type: "TEXT", nullable: false),
                    High = table.Column<decimal>(type: "TEXT", nullable: false),
                    Low = table.Column<decimal>(type: "TEXT", nullable: false),
                    Close = table.Column<decimal>(type: "TEXT", nullable: false),
                    Volume = table.Column<decimal>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceBars", x => new { x.TenantId, x.AssetId, x.Resolution, x.OpenTimeUtc });
                });

            migrationBuilder.CreateTable(
                name: "TickLiquiditySnapshots",
                columns: table => new
                {
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    PoolId = table.Column<string>(type: "TEXT", nullable: false),
                    AsOfUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    Tick = table.Column<int>(type: "INTEGER", nullable: false),
                    LiquidityNet = table.Column<string>(type: "TEXT", nullable: false),
                    LiquidityGross = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TickLiquiditySnapshots", x => new { x.TenantId, x.PoolId, x.AsOfUtc, x.Tick });
                });

            migrationBuilder.CreateTable(
                name: "Wallets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    Chains = table.Column<string>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wallets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DecisionAnnotations_DecisionLogEntryId",
                table: "DecisionAnnotations",
                column: "DecisionLogEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_DecisionLogEntries_TenantId_PoolId",
                table: "DecisionLogEntries",
                columns: new[] { "TenantId", "PoolId" });

            migrationBuilder.CreateIndex(
                name: "IX_IntentRecords_TenantId_PositionId",
                table: "IntentRecords",
                columns: new[] { "TenantId", "PositionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertRules");

            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "Assets");

            migrationBuilder.DropTable(
                name: "AuditReports");

            migrationBuilder.DropTable(
                name: "BacktestRuns");

            migrationBuilder.DropTable(
                name: "Chains");

            migrationBuilder.DropTable(
                name: "DecisionAnnotations");

            migrationBuilder.DropTable(
                name: "DecisionLogEntries");

            migrationBuilder.DropTable(
                name: "DexProtocols");

            migrationBuilder.DropTable(
                name: "IntentRecords");

            migrationBuilder.DropTable(
                name: "Pools");

            migrationBuilder.DropTable(
                name: "PoolSnapshots");

            migrationBuilder.DropTable(
                name: "PositionEvents");

            migrationBuilder.DropTable(
                name: "Positions");

            migrationBuilder.DropTable(
                name: "PriceBars");

            migrationBuilder.DropTable(
                name: "TickLiquiditySnapshots");

            migrationBuilder.DropTable(
                name: "Wallets");
        }
    }
}
