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
                    table.ForeignKey(
                        name: "FK_PriceBars_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    table.ForeignKey(
                        name: "FK_DexProtocols_Chains_ChainId",
                        column: x => x.ChainId,
                        principalTable: "Chains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    table.ForeignKey(
                        name: "FK_AuditReports_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    table.ForeignKey(
                        name: "FK_Pools_Assets_Token0AssetId",
                        column: x => x.Token0AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pools_Assets_Token1AssetId",
                        column: x => x.Token1AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pools_Chains_ChainId",
                        column: x => x.ChainId,
                        principalTable: "Chains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pools_DexProtocols_DexProtocolId",
                        column: x => x.DexProtocolId,
                        principalTable: "DexProtocols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    table.ForeignKey(
                        name: "FK_DecisionLogEntries_Pools_PoolId",
                        column: x => x.PoolId,
                        principalTable: "Pools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    table.ForeignKey(
                        name: "FK_PoolSnapshots_Pools_PoolId",
                        column: x => x.PoolId,
                        principalTable: "Pools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    table.ForeignKey(
                        name: "FK_PositionEvents_Pools_PoolId",
                        column: x => x.PoolId,
                        principalTable: "Pools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PositionEvents_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    table.ForeignKey(
                        name: "FK_Positions_Pools_PoolId",
                        column: x => x.PoolId,
                        principalTable: "Pools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Positions_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    table.ForeignKey(
                        name: "FK_TickLiquiditySnapshots_Pools_PoolId",
                        column: x => x.PoolId,
                        principalTable: "Pools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    table.ForeignKey(
                        name: "FK_DecisionAnnotations_DecisionLogEntries_DecisionLogEntryId",
                        column: x => x.DecisionLogEntryId,
                        principalTable: "DecisionLogEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    table.ForeignKey(
                        name: "FK_IntentRecords_IntentRecords_SupersedesIntentRecordId",
                        column: x => x.SupersedesIntentRecordId,
                        principalTable: "IntentRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntentRecords_Positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "Positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditReports_WalletId",
                table: "AuditReports",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_DecisionAnnotations_DecisionLogEntryId",
                table: "DecisionAnnotations",
                column: "DecisionLogEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_DecisionLogEntries_PoolId",
                table: "DecisionLogEntries",
                column: "PoolId");

            migrationBuilder.CreateIndex(
                name: "IX_DecisionLogEntries_TenantId_PoolId",
                table: "DecisionLogEntries",
                columns: new[] { "TenantId", "PoolId" });

            migrationBuilder.CreateIndex(
                name: "IX_DexProtocols_ChainId",
                table: "DexProtocols",
                column: "ChainId");

            migrationBuilder.CreateIndex(
                name: "IX_IntentRecords_PositionId",
                table: "IntentRecords",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_IntentRecords_SupersedesIntentRecordId",
                table: "IntentRecords",
                column: "SupersedesIntentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_IntentRecords_TenantId_PositionId",
                table: "IntentRecords",
                columns: new[] { "TenantId", "PositionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Pools_ChainId",
                table: "Pools",
                column: "ChainId");

            migrationBuilder.CreateIndex(
                name: "IX_Pools_DexProtocolId",
                table: "Pools",
                column: "DexProtocolId");

            migrationBuilder.CreateIndex(
                name: "IX_Pools_Token0AssetId",
                table: "Pools",
                column: "Token0AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Pools_Token1AssetId",
                table: "Pools",
                column: "Token1AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_PoolSnapshots_PoolId",
                table: "PoolSnapshots",
                column: "PoolId");

            migrationBuilder.CreateIndex(
                name: "IX_PositionEvents_PoolId",
                table: "PositionEvents",
                column: "PoolId");

            migrationBuilder.CreateIndex(
                name: "IX_PositionEvents_WalletId",
                table: "PositionEvents",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_PoolId",
                table: "Positions",
                column: "PoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_WalletId",
                table: "Positions",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceBars_AssetId",
                table: "PriceBars",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_TickLiquiditySnapshots_PoolId",
                table: "TickLiquiditySnapshots",
                column: "PoolId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertRules");

            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "AuditReports");

            migrationBuilder.DropTable(
                name: "BacktestRuns");

            migrationBuilder.DropTable(
                name: "DecisionAnnotations");

            migrationBuilder.DropTable(
                name: "IntentRecords");

            migrationBuilder.DropTable(
                name: "PoolSnapshots");

            migrationBuilder.DropTable(
                name: "PositionEvents");

            migrationBuilder.DropTable(
                name: "PriceBars");

            migrationBuilder.DropTable(
                name: "TickLiquiditySnapshots");

            migrationBuilder.DropTable(
                name: "DecisionLogEntries");

            migrationBuilder.DropTable(
                name: "Positions");

            migrationBuilder.DropTable(
                name: "Pools");

            migrationBuilder.DropTable(
                name: "Wallets");

            migrationBuilder.DropTable(
                name: "Assets");

            migrationBuilder.DropTable(
                name: "DexProtocols");

            migrationBuilder.DropTable(
                name: "Chains");
        }
    }
}
