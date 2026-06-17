// FollowAlpha.LP.Api — ASP.NET Core minimal API host (composition root).
//
// Phase 3.1 is API *foundation*, not product: auth, error contract, OpenAPI, health. The Range Advisor
// surface (assets/pools/ranges/decisions) lands in 3.2-3.6. The two endpoints under the secured group are
// minimal foundation probes (auth + the 422 insufficient-data path) and are replaced by real use-case
// endpoints in 3.2+.
using FollowAlpha.LP.Api.Errors;
using FollowAlpha.LP.Api.Security;
using FollowAlpha.LP.Application.Errors;
using Microsoft.AspNetCore.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// RFC 7807 problem+json for every error response (API-CONTRACT §2) + the 422 insufficient-data mapping.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<InsufficientDataExceptionHandler>();

// OpenAPI document (TECH-STACK §1). Served at /openapi/v1.json; the generated spec is the living contract.
// The temporary /v1/_diagnostics/* probes are flagged deprecated via an operation transformer (WithOpenApi
// is removed in .NET 10), so they read as non-product even if someone finds them in the spec.
builder.Services.AddOpenApi("v1", options =>
    options.AddOperationTransformer((operation, context, _) =>
    {
        if (context.Description.RelativePath?.StartsWith("v1/_diagnostics", StringComparison.OrdinalIgnoreCase) == true)
        {
            operation.Deprecated = true;
        }

        return Task.CompletedTask;
    }));

var app = builder.Build();

app.UseExceptionHandler();
app.MapOpenApi();

// Liveness/readiness for ops — intentionally unauthenticated and a real endpoint (never deprecated). The
// full collector-freshness /v1/health (API-CONTRACT §3) is wired when the snapshot store is consumed in a
// later phase; this is the skeleton.
app.MapGet("/v1/health", () => Results.Ok(new { status = "ok", timeUtc = DateTimeOffset.UtcNow }))
    .WithName("Health");

// Everything else under /v1 requires the X-Api-Key (API-CONTRACT §1; NFR S3). Real product endpoints land in 3.2+.
var secured = app.MapGroup("/v1").AddEndpointFilter<ApiKeyEndpointFilter>();

// Temporary Phase-3.1 plumbing probes — NOT product endpoints. They only exist to prove the auth seam and
// the RFC7807/422 path while no real use-case endpoints exist yet. Exposed in Development/Testing only (so
// they never become accidental public API in Production) and flagged deprecated in the OpenAPI document.
// They are removed when the real /v1 endpoints arrive in 3.2+.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    MapDiagnosticProbes(secured);
}

app.Run();

static void MapDiagnosticProbes(RouteGroupBuilder secured)
{
    var diagnostics = secured.MapGroup("/_diagnostics");

    // Auth accept-path probe: 200 only with a valid X-Api-Key.
    diagnostics.MapGet("/ping", () => Results.Ok(new { pong = true }))
        .WithName("DiagnosticsPing")
        .WithSummary("[temporary 3.1 probe] auth plumbing — not a product endpoint")
        .WithDescription("Phase-3.1 scaffolding to prove the X-Api-Key seam (200 only with a valid key). "
            + "Not part of the product API; removed when real /v1 endpoints land in 3.2+.");

    // 422 insufficient-data path probe (RN-02; API-CONTRACT §2). Real use cases throw this from their own
    // logic in 3.2+; this stands in until they exist.
    diagnostics.MapGet("/insufficient-data", IResult () =>
            throw new InsufficientDataException(
                "Not enough collected data to produce a result (temporary 3.1 probe).",
                ["priceBars", "poolSnapshot"]))
        .WithName("DiagnosticsInsufficientData")
        .WithSummary("[temporary 3.1 probe] RFC7807/422 plumbing — not a product endpoint")
        .WithDescription("Phase-3.1 scaffolding to prove the insufficient-data path returns RFC7807 422. "
            + "Not part of the product API; removed when real /v1 endpoints land in 3.2+.");
}

// Exposed so the integration tests can host the app in-memory via WebApplicationFactory<Program>.
public partial class Program;
