// FollowAlpha.LP.Api — ASP.NET Core minimal API host (composition root).
// Phase 0 skeleton: builds and runs an empty pipeline. Auth, OpenAPI and Range Advisor endpoints land in Phase 3.

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.Run();
