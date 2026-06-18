using FollowAlpha.LP.Application.Abstractions;
using FollowAlpha.LP.Application.Collection;
using Microsoft.Extensions.Options;

namespace FollowAlpha.LP.DataSync.Jobs;

/// <summary>
/// Scheduled pool/tick snapshot job: runs <see cref="IngestPoolSnapshots"/> over the configured watchlist.
/// Resolves the use case in a fresh DI scope per run (the EF context + stores are scoped).
/// </summary>
public sealed class PoolSnapshotJob : CronBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<DataSyncOptions> _options;
    private readonly DataSyncHealth _health;

    public PoolSnapshotJob(
        IServiceScopeFactory scopeFactory,
        IOptions<DataSyncOptions> options,
        IClock clock,
        DataSyncHealth health,
        ILogger<PoolSnapshotJob> logger)
        : base(options.Value.PoolSnapshotCron, options.Value.RunJobsOnStartup, clock, logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _health = health;
    }

    protected override string JobName => "pool-snapshot";

    protected override async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var pools = _options.Value.Watchlist
            .Select(p => new PoolToSnapshot(p.PoolId, p.ChainId, p.Address))
            .ToList();
        if (pools.Count == 0)
        {
            Logger.LogWarning("Pool snapshot job has an empty watchlist; nothing to do.");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var useCase = scope.ServiceProvider.GetRequiredService<IngestPoolSnapshots>();
        var outcomes = await useCase.RunAsync(pools, cancellationToken);

        foreach (var o in outcomes)
        {
            if (o.Error is not null)
            {
                Logger.LogError("Pool {Pool} snapshot failed: {Error}", o.PoolId, o.Error);
            }
            else
            {
                Logger.LogInformation(
                    "Pool {Pool}: snapshot {Inserted}, {TickRows} tick rows.",
                    o.PoolId, o.PoolSnapshotInserted ? "inserted" : "already-present", o.TickRowsInserted);
            }
        }

        _health.RecordRun(JobName, Now);
    }
}
