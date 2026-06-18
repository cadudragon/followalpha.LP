using Cronos;
using FollowAlpha.LP.Application.Abstractions;

namespace FollowAlpha.LP.DataSync.Jobs;

/// <summary>
/// A <see cref="BackgroundService"/> that runs a job on a cron schedule (Cronos), optionally once at
/// startup. The host stays thin: scheduling and error isolation live here; the actual work is a use case
/// resolved per run (ARCHITECTURE.md §7). A single failed run is logged and never tears down the host —
/// the DataSync worker must keep capturing the irrecoverable tick distribution.
/// </summary>
public abstract class CronBackgroundService(string cronExpression, bool runOnStartup, IClock clock, ILogger logger)
    : BackgroundService
{
    private readonly CronExpression _cron = CronExpression.Parse(cronExpression);

    /// <summary>The host clock instant (UTC) — derived jobs use this instead of capturing their own clock.</summary>
    protected DateTimeOffset Now => clock.UtcNow;

    /// <summary>The job logger — derived jobs use this instead of capturing their own logger.</summary>
    protected ILogger Logger => logger;

    protected abstract string JobName { get; }

    protected abstract Task RunOnceAsync(CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Job {Job} scheduled with cron '{Cron}'.", JobName, cronExpression);

        if (runOnStartup)
        {
            await SafeRunAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var next = _cron.GetNextOccurrence(clock.UtcNow.UtcDateTime, TimeZoneInfo.Utc);
            if (next is null)
            {
                logger.LogWarning("Job {Job} has no further scheduled occurrences; stopping its loop.", JobName);
                return;
            }

            var delay = next.Value - clock.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            await SafeRunAsync(stoppingToken);
        }
    }

    private async Task SafeRunAsync(CancellationToken cancellationToken)
    {
        try
        {
            var started = clock.UtcNow;
            logger.LogInformation("Job {Job} starting.", JobName);
            await RunOnceAsync(cancellationToken);
            logger.LogInformation("Job {Job} finished in {Elapsed}.", JobName, clock.UtcNow - started);
        }
        catch (OperationCanceledException)
        {
            // host shutting down
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job {Job} failed; will retry on the next scheduled occurrence.", JobName);
        }
    }
}
