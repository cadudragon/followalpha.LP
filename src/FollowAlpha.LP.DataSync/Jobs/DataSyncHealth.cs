using System.Collections.Concurrent;

namespace FollowAlpha.LP.DataSync.Jobs;

/// <summary>
/// Shared health state for the DataSync (NFR O2/A3): the last successful run time per job, surfaced by
/// <c>/health</c> alongside per-pool snapshot freshness. Thread-safe (jobs run on background threads).
/// </summary>
public sealed class DataSyncHealth
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastRunByJob = new();

    public void RecordRun(string job, DateTimeOffset at) => _lastRunByJob[job] = at;

    public DateTimeOffset? GetLastRun(string job) =>
        _lastRunByJob.TryGetValue(job, out var at) ? at : null;
}
