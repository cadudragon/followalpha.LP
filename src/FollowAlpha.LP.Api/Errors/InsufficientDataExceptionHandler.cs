using FollowAlpha.LP.Application.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FollowAlpha.LP.Api.Errors;

/// <summary>
/// Translates the Application's <see cref="InsufficientDataException"/> into HTTP <c>422</c> as RFC 7807
/// problem+json (API-CONTRACT §2 — "insufficient data … NOT a guess"), listing the missing data under a
/// <c>missing</c> extension. Other exceptions are left for the framework's default handler (→ 500), so we
/// never accidentally mask a real fault as a data-sufficiency problem.
/// </summary>
internal sealed class InsufficientDataExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not InsufficientDataException insufficient)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = insufficient,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Insufficient data",
                Detail = insufficient.Message,
                Type = "https://httpstatuses.io/422",
                Extensions = { ["missing"] = insufficient.Missing },
            },
        });
    }
}
