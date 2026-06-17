namespace FollowAlpha.LP.Application.Errors;

/// <summary>
/// Thrown by a use case when the collected data is too thin to produce an honest verdict/estimate
/// (RN-02). It is <b>not</b> an error condition to paper over with a guess — the host maps it to HTTP
/// <c>422 Unprocessable Entity</c> and surfaces <see cref="Missing"/> so the caller knows exactly what
/// is absent (API-CONTRACT §2). Lives in Application because "insufficient data" is a domain-of-discourse
/// outcome of the use cases, not an HTTP concept; the translation to 422 is the host's job.
/// </summary>
public sealed class InsufficientDataException : Exception
{
    public InsufficientDataException(string message, IReadOnlyList<string> missing)
        : base(message) => Missing = missing;

    public InsufficientDataException(string message)
        : this(message, []) { }

    /// <summary>The specific data the use case needed but did not have (e.g. <c>"priceBars"</c>, <c>"poolSnapshot"</c>).</summary>
    public IReadOnlyList<string> Missing { get; }
}
