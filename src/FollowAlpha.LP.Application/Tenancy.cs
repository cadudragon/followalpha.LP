namespace FollowAlpha.LP.Application;

/// <summary>
/// Multi-tenant seam (ARCHITECTURE.md §8): every persisted aggregate carries a <c>TenantId</c>. Today
/// there is one constant tenant; real per-tenant isolation is the SaaS gate.
/// </summary>
public static class Tenancy
{
    /// <summary>The single tenant in v1.</summary>
    public const string DefaultTenantId = "default";
}
