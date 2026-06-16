namespace FollowAlpha.LP.Application.Abstractions;

/// <summary>
/// The clock port (ARCHITECTURE.md §5). The Domain never reads the clock; everything outside it takes
/// time through this seam so time-dependent behavior stays testable and deterministic.
/// </summary>
public interface IClock
{
    /// <summary>The current instant in UTC.</summary>
    DateTimeOffset UtcNow { get; }
}
