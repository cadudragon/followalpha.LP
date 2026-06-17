// FollowAlpha.LP.Api — ASP.NET Core minimal API host (composition root).
//
// Phase 3.2 adds the first real product surface: asset/pool exploration (UC-02), read-only over the data the
// Collector persists. The Range Advisor (ranges/verdict/backtest/decision-log) lands in 3.3-3.6. The API is
// a reader: it never migrates or writes the database (the Collector owns that).
using FollowAlpha.LP.Api.Errors;
using FollowAlpha.LP.Api.Security;
using FollowAlpha.LP.Application.Abstractions;
using FollowAlpha.LP.Application.Exploration;
using FollowAlpha.LP.Application.Persistence;
using FollowAlpha.LP.Infrastructure.Persistence;
using FollowAlpha.LP.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// RFC 7807 problem+json for every error response (API-CONTRACT §2) + the 422 insufficient-data mapping.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<InsufficientDataExceptionHandler>();

// OpenAPI document (TECH-STACK §1). Served at /openapi/v1.json; the generated spec is the living contract.
builder.Services.AddOpenApi("v1");

// Persistence — read-only over the Collector's SQLite DB (env/appsettings; the API never migrates it).
var dbPath = builder.Configuration["LP_DB_PATH"] ?? "./data/followalpha-lp.db";
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={dbPath};Foreign Keys=True"));
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton(ExplorationPolicy.Default);
builder.Services.AddScoped<ISnapshotStore, EfSnapshotStore>();
builder.Services.AddScoped<IPriceStore, EfPriceStore>();
builder.Services.AddScoped<IExplorationReadStore, EfExplorationReadStore>();

// Exploration use cases (UC-02).
builder.Services.AddScoped<ListWatchlistAssets>();
builder.Services.AddScoped<GetAssetChart>();
builder.Services.AddScoped<ClassifyAssetRegime>();
builder.Services.AddScoped<ListAssetPools>();
builder.Services.AddScoped<GetPoolDetail>();

var app = builder.Build();

app.UseExceptionHandler();
app.MapOpenApi();

// Liveness/readiness for ops — intentionally unauthenticated and a real endpoint. The full
// collector-freshness /v1/health (API-CONTRACT §3) is wired in a later phase; this is the skeleton.
app.MapGet("/v1/health", () => Results.Ok(new { status = "ok", timeUtc = DateTimeOffset.UtcNow }))
    .WithName("Health");

// Everything under /v1 requires the X-Api-Key (API-CONTRACT §1; NFR S3).
var v1 = app.MapGroup("/v1").AddEndpointFilter<ApiKeyEndpointFilter>();

// Asset/pool exploration (UC-02). 404 (problem+json) for unknown asset/pool; 422 for thin/stale data.
v1.MapGet("/assets", async (ListWatchlistAssets useCase, CancellationToken ct) =>
        Results.Ok(await useCase.RunAsync(ct)))
    .WithName("ListAssets");

v1.MapGet("/assets/{id}/chart", async (string id, GetAssetChart useCase, CancellationToken ct) =>
        await useCase.RunAsync(id, ct) is { } result ? Results.Ok(result) : NotFound("asset", id))
    .WithName("AssetChart");

v1.MapGet("/assets/{id}/regime", async (string id, ClassifyAssetRegime useCase, CancellationToken ct) =>
        await useCase.RunAsync(id, ct) is { } result ? Results.Ok(result) : NotFound("asset", id))
    .WithName("AssetRegime");

v1.MapGet("/assets/{id}/pools", async (string id, ListAssetPools useCase, CancellationToken ct) =>
        await useCase.RunAsync(id, ct) is { } result ? Results.Ok(result) : NotFound("asset", id))
    .WithName("AssetPools");

v1.MapGet("/pools/{poolId}", async (string poolId, GetPoolDetail useCase, CancellationToken ct) =>
        await useCase.RunAsync(poolId, ct) is { } result ? Results.Ok(result) : NotFound("pool", poolId))
    .WithName("PoolDetail");

app.Run();

// Unknown asset/pool -> 404 as RFC 7807 problem+json (API-CONTRACT §2).
static IResult NotFound(string kind, string id) => Results.Problem(
    statusCode: StatusCodes.Status404NotFound,
    title: "Not found",
    detail: $"No {kind} with id '{id}'.",
    type: "https://httpstatuses.io/404");

// Exposed so the integration tests can host the app in-memory via WebApplicationFactory<Program>.
public partial class Program;
