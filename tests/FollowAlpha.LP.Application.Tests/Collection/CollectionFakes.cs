using FollowAlpha.LP.Application.Abstractions;
using FollowAlpha.LP.Application.ChainEvents;
using FollowAlpha.LP.Application.Persistence;
using FollowAlpha.LP.Application.Pools;
using FollowAlpha.LP.Application.Prices;

namespace FollowAlpha.LP.Application.Tests.Collection;

internal sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

internal sealed class FakePoolDataSource : IPoolDataSource
{
    public PoolState State { get; set; } = new("0xpool", 0, "79228162514264337593543950336", "1000", 500, 100m);

    public List<PoolDayVolume> DayVolumes { get; } = [];

    public List<TickLiquidity> Ticks { get; } = [];

    public Func<string, Exception?>? FailFor { get; set; }

    public Task<PoolState> GetPoolStateAsync(string chainId, string poolAddress, CancellationToken cancellationToken = default)
    {
        if (FailFor?.Invoke(poolAddress) is { } ex)
        {
            throw ex;
        }

        return Task.FromResult(State);
    }

    public Task<IReadOnlyList<PoolDayVolume>> GetDayVolumesAsync(string chainId, string poolAddress, int days, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PoolDayVolume>>(DayVolumes);

    public Task<IReadOnlyList<TickLiquidity>> GetTickLiquidityAsync(string chainId, string poolAddress, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TickLiquidity>>(Ticks);
}

internal sealed class InMemorySnapshotStore : ISnapshotStore
{
    public List<PoolSnapshot> PoolSnapshots { get; } = [];

    public List<TickLiquiditySnapshot> TickSnapshots { get; } = [];

    public Task<bool> InsertPoolSnapshotIfAbsentAsync(PoolSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (PoolSnapshots.Any(x => x.TenantId == snapshot.TenantId && x.PoolId == snapshot.PoolId && x.AsOfUtc == snapshot.AsOfUtc))
        {
            return Task.FromResult(false);
        }

        PoolSnapshots.Add(snapshot);
        return Task.FromResult(true);
    }

    public Task<bool> InsertTickLiquiditySnapshotIfAbsentAsync(TickLiquiditySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (TickSnapshots.Any(x => x.TenantId == snapshot.TenantId && x.PoolId == snapshot.PoolId && x.AsOfUtc == snapshot.AsOfUtc && x.Tick == snapshot.Tick))
        {
            return Task.FromResult(false);
        }

        TickSnapshots.Add(snapshot);
        return Task.FromResult(true);
    }

    public Task<PoolSnapshot?> GetLatestPoolSnapshotAsync(string tenantId, string poolId, CancellationToken cancellationToken = default) =>
        Task.FromResult(PoolSnapshots.Where(x => x.TenantId == tenantId && x.PoolId == poolId).OrderByDescending(x => x.AsOfUtc).FirstOrDefault());

    public Task<IReadOnlyList<TickLiquiditySnapshot>> GetTickLiquidityAsync(string tenantId, string poolId, DateTimeOffset asOfUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TickLiquiditySnapshot>>([.. TickSnapshots.Where(x => x.TenantId == tenantId && x.PoolId == poolId && x.AsOfUtc == asOfUtc)]);
}

internal sealed class FakeChainEventReader : IChainEventReader
{
    public List<string> TokenIds { get; } = [];

    public List<WalletNftTransfer> Transfers { get; } = [];

    public List<ChainPositionEvent> Events { get; } = [];

    public long ChainHead { get; set; } = 1000;

    public int ReadCallCount { get; private set; }

    public long? LastReadFromBlock { get; private set; }

    public Task<IReadOnlyList<ChainPositionEvent>> ReadPositionEventsAsync(string chainId, IReadOnlyCollection<string> tokenIds, long fromBlock, long toBlock, CancellationToken cancellationToken = default)
    {
        ReadCallCount++;
        LastReadFromBlock = fromBlock;
        // Mirror production: only events in the requested window are returned.
        return Task.FromResult<IReadOnlyList<ChainPositionEvent>>(
            [.. Events.Where(e => tokenIds.Contains(e.TokenId) && e.BlockNumber >= fromBlock && e.BlockNumber <= toBlock)]);
    }

    public Task<IReadOnlyList<string>> DiscoverWalletTokenIdsAsync(string chainId, string walletAddress, long fromBlock, long toBlock, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(TokenIds);

    public Task<IReadOnlyList<WalletNftTransfer>> DiscoverWalletTransfersAsync(string chainId, string walletAddress, long fromBlock, long toBlock, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WalletNftTransfer>>(
            [.. Transfers.Where(t => t.BlockNumber >= fromBlock && t.BlockNumber <= toBlock)]);

    public Task<long> GetChainHeadBlockAsync(string chainId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ChainHead);
}

internal sealed class InMemoryWalletOwnershipStore : IWalletOwnershipStore
{
    public List<WalletPositionOwnership> Intervals { get; } = [];

    public Task<IReadOnlyList<WalletPositionOwnership>> GetByWalletAsync(string tenantId, string chainId, string walletId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WalletPositionOwnership>>(
            [.. Intervals.Where(x => x.TenantId == tenantId && x.ChainId == chainId && x.WalletId == walletId)]);

    public Task UpsertAsync(WalletPositionOwnership interval, CancellationToken cancellationToken = default)
    {
        var exists = Intervals.Any(x => x.ChainId == interval.ChainId && x.WalletId == interval.WalletId && x.TokenId == interval.TokenId && x.Seq == interval.Seq);
        if (!exists)
        {
            Intervals.Add(interval);
        }

        return Task.CompletedTask;
    }
}

internal sealed class InMemoryWalletSyncCursorStore : IWalletSyncCursorStore
{
    public List<WalletSyncCursor> Cursors { get; } = [];

    public Task<WalletSyncCursor?> GetAsync(string tenantId, string chainId, string walletId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Cursors.FirstOrDefault(x => x.TenantId == tenantId && x.ChainId == chainId && x.WalletId == walletId));

    public Task SetAsync(WalletSyncCursor cursor, CancellationToken cancellationToken = default)
    {
        Cursors.RemoveAll(x => x.ChainId == cursor.ChainId && x.WalletId == cursor.WalletId);
        Cursors.Add(cursor);
        return Task.CompletedTask;
    }
}

internal sealed class FakePriceSeriesSource : IPriceSeriesSource
{
    public List<AssetUsdBar> Bars { get; } = [];

    public Func<string, Exception?>? FailFor { get; set; }

    public Task<IReadOnlyList<AssetUsdBar>> GetDailyUsdBarsAsync(string chainId, string tokenAddress, int days, CancellationToken cancellationToken = default)
    {
        if (FailFor?.Invoke(tokenAddress) is { } ex)
        {
            throw ex;
        }

        return Task.FromResult<IReadOnlyList<AssetUsdBar>>(Bars);
    }
}

internal sealed class InMemoryPriceStore : IPriceStore
{
    public List<PriceBar> Bars { get; } = [];

    public Task<bool> InsertIfAbsentAsync(PriceBar bar, CancellationToken cancellationToken = default)
    {
        if (Bars.Any(x => x.TenantId == bar.TenantId && x.AssetId == bar.AssetId && x.Resolution == bar.Resolution && x.OpenTimeUtc == bar.OpenTimeUtc))
        {
            return Task.FromResult(false);
        }

        Bars.Add(bar);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<PriceBar>> GetByAssetAsync(string tenantId, string assetId, string resolution, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PriceBar>>([.. Bars.Where(x => x.TenantId == tenantId && x.AssetId == assetId && x.Resolution == resolution)]);
}

internal sealed class FakePositionStateReader : IPositionStateReader
{
    public Dictionary<string, ChainPositionState> StateByToken { get; } = [];

    public string PoolAddress { get; set; } = "0xpool";

    public Dictionary<string, int> DecimalsByToken { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task<ChainPositionState> GetPositionStateAsync(string chainId, string tokenId, CancellationToken cancellationToken = default) =>
        Task.FromResult(StateByToken[tokenId]);

    public Task<string> GetPoolAddressAsync(string chainId, string token0, string token1, int feeTier, CancellationToken cancellationToken = default) =>
        Task.FromResult(PoolAddress);

    public Task<int> GetTokenDecimalsAsync(string chainId, string tokenAddress, CancellationToken cancellationToken = default) =>
        Task.FromResult(DecimalsByToken[tokenAddress]);
}

internal sealed class InMemoryPositionEventStore : IPositionEventStore
{
    public List<PositionEvent> Events { get; } = [];

    public Task<bool> InsertIfAbsentAsync(PositionEvent positionEvent, CancellationToken cancellationToken = default)
    {
        if (Events.Any(x => x.TenantId == positionEvent.TenantId && x.ChainId == positionEvent.ChainId && x.TxHash == positionEvent.TxHash && x.LogIndex == positionEvent.LogIndex))
        {
            return Task.FromResult(false);
        }

        Events.Add(positionEvent);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<PositionEvent>> GetByWalletAsync(string tenantId, string walletId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PositionEvent>>([.. Events.Where(x => x.TenantId == tenantId && x.WalletId == walletId)]);
}
