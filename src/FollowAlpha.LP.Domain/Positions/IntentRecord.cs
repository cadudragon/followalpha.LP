namespace FollowAlpha.LP.Domain.Positions;

/// <summary>
/// One immutable intent declaration: the intent, when it was assigned (a value supplied by the caller —
/// the Domain never reads the clock), and the reason. Reclassification appends a new record; the
/// original is never mutated (LP-KNOWLEDGE.md §3 — the system never forgets the original thesis).
/// </summary>
public readonly record struct IntentRecord
{
    /// <summary>Constructs an intent record.</summary>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is null or blank.</exception>
    public IntentRecord(Intent intent, DateTimeOffset assignedAtUtc, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("An intent record must carry a reason.", nameof(reason));
        }

        if (assignedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("AssignedAtUtc must be UTC (zero offset).", nameof(assignedAtUtc));
        }

        Intent = intent;
        AssignedAtUtc = assignedAtUtc;
        Reason = reason;
    }

    /// <summary>The declared intent.</summary>
    public Intent Intent { get; }

    /// <summary>When the intent was assigned (UTC, supplied by the caller).</summary>
    public DateTimeOffset AssignedAtUtc { get; }

    /// <summary>Why this intent was assigned (required, including for reclassification).</summary>
    public string Reason { get; }
}
