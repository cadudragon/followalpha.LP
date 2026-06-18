using FollowAlpha.LP.Application.Abstractions;
using FollowAlpha.LP.Application.Collection;
using FollowAlpha.LP.Application.Pools;
using Microsoft.Extensions.Options;

namespace FollowAlpha.LP.DataSync.Jobs;

/// <summary>
/// Scheduled price-series refresh: runs <see cref="IngestPriceSeries"/> over the distinct tokens of the
/// configured watchlist pools, persisting daily USD bars (<see cref="IngestPriceSeries.Resolution"/>).
/// Resolves the use case in a fresh DI scope per run (the EF context + stores are scoped). Daily bars are
/// recoverable, so a missed run is not fatal.
/// </summary>
public sealed class PriceRefreshJob : CronBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<DataSyncOptions> _options;
    private readonly DataSyncHealth _health;

    public PriceRefreshJob(
        IServiceScopeFactory scopeFactory,
        IOptions<DataSyncOptions> options,
        IClock clock,
        DataSyncHealth health,
        ILogger<PriceRefreshJob> logger)
        : base(options.Value.PriceRefreshCron, options.Value.RunJobsOnStartup, clock, logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _health = health;
    }

    protected override string JobName => "price-refresh";

    protected override async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var assets = _options.Value.Watchlist
            .SelectMany(p => new[]
            {
                new AssetToPrice(AssetIdentity.For(p.ChainId, p.Token0.Address), p.ChainId, p.Token0.Address),
                new AssetToPrice(AssetIdentity.For(p.ChainId, p.Token1.Address), p.ChainId, p.Token1.Address),
            })
            .DistinctBy(a => a.AssetId)
            .ToList();
        if (assets.Count == 0)
        {
            Logger.LogWarning("Price refresh job has an empty watchlist; nothing to do.");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var useCase = scope.ServiceProvider.GetRequiredService<IngestPriceSeries>();
        var outcomes = await useCase.RunAsync(assets, _options.Value.PriceRefreshDays, cancellationToken);

        foreach (var o in outcomes)
        {
            if (o.Error is not null)
            {
                Logger.LogError("Asset {Asset} price refresh failed: {Error}", o.AssetId, o.Error);
            }
            else
            {
                Logger.LogInformation("Asset {Asset}: {Read} bars read, {Inserted} inserted.", o.AssetId, o.BarsRead, o.BarsInserted);
            }
        }

        _health.RecordRun(JobName, Now);
    }
}
