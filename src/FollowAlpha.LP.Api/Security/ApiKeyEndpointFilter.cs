using System.Security.Cryptography;
using System.Text;

namespace FollowAlpha.LP.Api.Security;

/// <summary>
/// Requires a valid <c>X-Api-Key</c> header (API-CONTRACT §1; NFR S3). Single key today (<c>LP_API_KEY</c>,
/// from env/user-secrets — never the repo), an identity seam for SaaS later. Fails closed: a missing
/// configured key, a missing header, or a mismatch all yield <c>401</c> as RFC 7807 problem+json. The
/// comparison is constant-time so a wrong key cannot be probed byte-by-byte.
/// </summary>
internal sealed class ApiKeyEndpointFilter(IConfiguration configuration) : IEndpointFilter
{
    public const string HeaderName = "X-Api-Key";
    private const string ConfigKey = "LP_API_KEY";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var configured = configuration[ConfigKey];
        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();

        if (!IsValid(configured, provided))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: $"A valid {HeaderName} header is required.",
                type: "https://httpstatuses.io/401");
        }

        return await next(context);
    }

    private static bool IsValid(string? configured, string? provided)
    {
        if (string.IsNullOrEmpty(configured) || string.IsNullOrEmpty(provided))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(configured));
    }
}
