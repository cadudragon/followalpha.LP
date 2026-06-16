namespace FollowAlpha.LP.Application.Persistence;

// Position projection (rebuildable from events), intent history (append-only), decision records
// (append-only), and analysis outputs (append-only) — DATA-MODEL.md §2.

/// <summary>
/// A position reconstructed from <see cref="PositionEvent"/>s — a fact-derived projection (rebuildable,
/// not hand-edited). Id = NFT token id + chain.
/// </summary>
public sealed class Position
{
    public required string Id { get; set; }
    public required string WalletId { get; set; }
    public required string PoolId { get; set; }
    public int TickLower { get; set; }
    public int TickUpper { get; set; }
    public DateTimeOffset OpenedAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public required string Status { get; set; }
    public string TenantId { get; set; } = Tenancy.DefaultTenantId;
}

/// <summary>
/// An intent declaration (append-only history; RN-01). Reclassification inserts a new row pointing at
/// the prior via <see cref="SupersedesIntentRecordId"/>; the original is never mutated. The "current"
/// intent is the latest record. Identity is <see cref="Id"/> (caller-supplied); not de-duplicated.
/// </summary>
public sealed class IntentRecord
{
    public Guid Id { get; set; }
    public required string PositionId { get; set; }
    public required string Intent { get; set; }
    public DateTimeOffset DeclaredAtUtc { get; set; }
    public string? Reason { get; set; }
    public Guid? SupersedesIntentRecordId { get; set; }
    public string TenantId { get; set; } = Tenancy.DefaultTenantId;
}

/// <summary>
/// An immutable decision-log entry — written on every evaluation, even if the user does not open
/// (RN-03). Identity is <see cref="Id"/>; entries are appended, never de-duplicated by
/// <see cref="ContentHash"/> (the hash is tamper-evidence/traceability, not an idempotency key —
/// two identical evaluations at different times are two events).
/// </summary>
public sealed class DecisionLogEntry
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public required string Kind { get; set; }
    public required string PoolId { get; set; }
    public string? Intent { get; set; }
    public decimal Capital { get; set; }
    public int TickLower { get; set; }
    public int TickUpper { get; set; }
    /// <summary>Full input snapshot (IV, forecast RV, fee APR, band survival, IL scenarios, regime).</summary>
    public required string InputsJson { get; set; }
    /// <summary>OPEN / DONT_OPEN, or null for channel sims.</summary>
    public string? Verdict { get; set; }
    public decimal ExpectancyNet { get; set; }
    /// <summary>SHA-256 of canonical inputs + verdict (tamper-evidence).</summary>
    public required string ContentHash { get; set; }
    public string TenantId { get; set; } = Tenancy.DefaultTenantId;
}

/// <summary>A dated note attached to a decision entry after the fact; never alters the entry (RN-03).</summary>
public sealed class DecisionAnnotation
{
    public Guid Id { get; set; }
    public Guid DecisionLogEntryId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public required string Text { get; set; }
    public string TenantId { get; set; } = Tenancy.DefaultTenantId;
}

/// <summary>A persisted descriptive replay result (UC-09). Deterministic: same params + data → same result.</summary>
public sealed class BacktestRun
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public required string Type { get; set; }
    public required string ParamsJson { get; set; }
    public required string ResultJson { get; set; }
    public DateTimeOffset DataWindowFromUtc { get; set; }
    public DateTimeOffset DataWindowToUtc { get; set; }
    public required string InputDataHash { get; set; }
    public string TenantId { get; set; } = Tenancy.DefaultTenantId;
}

/// <summary>A persisted wallet audit report (UC-01). Reproducible (byte-identical for same inputs).</summary>
public sealed class AuditReport
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public required string WalletId { get; set; }
    public required string ResultJson { get; set; }
    public required string InputDataHash { get; set; }
    public string TenantId { get; set; } = Tenancy.DefaultTenantId;
}
