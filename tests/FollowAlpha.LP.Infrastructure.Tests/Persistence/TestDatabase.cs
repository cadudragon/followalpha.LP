using FollowAlpha.LP.Application.Persistence;
using FollowAlpha.LP.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FollowAlpha.LP.Infrastructure.Tests.Persistence;

/// <summary>
/// An isolated SQLite database for one test: a private in-memory connection (foreign keys ON) with the
/// real migration applied (so the migration and its FKs are exercised), seeded with a minimal reference
/// graph (chain, dex, assets, pool, wallet) so dependent facts/records satisfy their foreign keys.
/// Torn down on dispose.
/// </summary>
internal sealed class TestDatabase : IDisposable
{
    public const string ChainId = "arbitrum";
    public const string DexId = "uniswap-v3";
    public const string EthId = "ETH";
    public const string UsdcId = "USDC";
    public const string PoolId = "pool1";
    public const string WalletId = "wallet1";

    private readonly SqliteConnection _connection;

    public TestDatabase()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new AppDbContext(options);
        Context.Database.Migrate();
        SeedReferenceData();
    }

    public AppDbContext Context { get; }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }

    private void SeedReferenceData()
    {
        Context.Chains.Add(new Chain { Id = ChainId, Name = "Arbitrum One", RpcEnvVarName = "RPC_URL_ARBITRUM", Enabled = true });
        Context.DexProtocols.Add(new DexProtocol { Id = DexId, ChainId = ChainId, SubgraphId = "sub", PositionManagerAddress = "0xpm", FeeTiers = "[500,3000]", Enabled = true });
        Context.Assets.Add(new Asset { Id = EthId, ChainId = ChainId, Address = "0xeth", Symbol = "ETH", Decimals = 18 });
        Context.Assets.Add(new Asset { Id = UsdcId, ChainId = ChainId, Address = "0xusdc", Symbol = "USDC", Decimals = 6 });
        Context.Pools.Add(new Pool
        {
            Id = PoolId, ChainId = ChainId, DexProtocolId = DexId, Token0AssetId = UsdcId, Token1AssetId = EthId,
            FeeTier = 3000, TickSpacing = 60, Address = "0xpool",
        });
        Context.Wallets.Add(new Wallet { Id = WalletId, Address = "0xwallet", Label = "main", Chains = "[\"arbitrum\"]" });
        Context.SaveChanges();
    }
}
