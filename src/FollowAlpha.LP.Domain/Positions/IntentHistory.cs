namespace FollowAlpha.LP.Domain.Positions;

/// <summary>
/// The append-only history of a position's intent. The original is preserved forever; reclassification
/// returns a new history with a record appended (the instance is immutable). When reclassified, the
/// position is flagged and its P&amp;L is shown against the benchmarks of <b>all</b> distinct intents it
/// has carried (LP-KNOWLEDGE.md §3; ARCHITECTURE.md §4.3).
/// </summary>
public sealed class IntentHistory
{
    private readonly IntentRecord[] _records;

    /// <summary>Starts a history from the original intent declaration.</summary>
    public IntentHistory(IntentRecord original)
        : this([original])
    {
    }

    private IntentHistory(IntentRecord[] records) => _records = records;

    /// <summary>The original (first) intent — never lost.</summary>
    public IntentRecord Original => _records[0];

    /// <summary>The current (most recent) intent.</summary>
    public IntentRecord Current => _records[^1];

    /// <summary>True once the intent has been reclassified at least once.</summary>
    public bool IsReclassified => _records.Length > 1;

    /// <summary>
    /// The full ordered history, oldest first. Returned as a read-only wrapper over the backing array,
    /// not the array itself — a caller cannot cast it back to <c>IntentRecord[]</c> and rewrite history
    /// (which would corrupt <see cref="Original"/>/<see cref="Current"/>). Append-only is the law here.
    /// </summary>
    public IReadOnlyList<IntentRecord> Records => Array.AsReadOnly(_records);

    /// <summary>Returns a new history with <paramref name="record"/> appended; this instance is unchanged.</summary>
    public IntentHistory Reclassify(IntentRecord record)
    {
        var next = new IntentRecord[_records.Length + 1];
        Array.Copy(_records, next, _records.Length);
        next[^1] = record;
        return new IntentHistory(next);
    }

    /// <summary>The distinct intents this position has carried, in first-seen order (drives dual-benchmark reporting).</summary>
    public IReadOnlyList<Intent> DistinctIntents()
    {
        var seen = new List<Intent>();
        foreach (var record in _records)
        {
            if (!seen.Contains(record.Intent))
            {
                seen.Add(record.Intent);
            }
        }

        return seen;
    }
}
