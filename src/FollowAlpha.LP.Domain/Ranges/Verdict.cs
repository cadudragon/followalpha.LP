namespace FollowAlpha.LP.Domain.Ranges;

/// <summary>
/// The range decision (AGENTS.md hard rule 5 naming): enum member <see cref="DoNotOpen"/>; the wire/DB
/// constant is <c>DONT_OPEN</c> and the UI label <c>DON'T OPEN</c> (mapped at those layers, not here).
/// </summary>
public enum Verdict
{
    /// <summary>Do not open the position. First member so <c>default(Verdict)</c> is fail-closed — never "open".</summary>
    DoNotOpen,

    /// <summary>Open the position.</summary>
    Open,
}
