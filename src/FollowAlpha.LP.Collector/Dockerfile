# FollowAlpha.LP Collector — always-on Worker host (ARCHITECTURE.md §7, IMPLEMENTATION-PLAN §2).
# Build from the REPO ROOT so the build context includes the props files and every referenced project:
#   docker build -f src/FollowAlpha.LP.Collector/Dockerfile -t followalpha-lp-collector .
#
# The Collector is the only deployable in Phase 2. It snapshots the irrecoverable tick-liquidity
# distribution continuously, so the SQLite database MUST live on a persistent volume (see DEPLOYMENT.md).

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore against the real package/version graph. Copy the solution-wide props + every csproj first so
# `dotnet restore` is cached independently of source churn.
# .editorconfig must be at the context root so the build honors the repo's analyzer rules (it is discovered
# by walking up from each source file). Without it, the image build applies bare SDK defaults, not ours.
COPY Directory.Build.props Directory.Packages.props NuGet.config .editorconfig ./
COPY src/FollowAlpha.LP.Domain/FollowAlpha.LP.Domain.csproj                 src/FollowAlpha.LP.Domain/
COPY src/FollowAlpha.LP.Application/FollowAlpha.LP.Application.csproj       src/FollowAlpha.LP.Application/
COPY src/FollowAlpha.LP.Infrastructure/FollowAlpha.LP.Infrastructure.csproj src/FollowAlpha.LP.Infrastructure/
COPY src/FollowAlpha.LP.Collector/FollowAlpha.LP.Collector.csproj           src/FollowAlpha.LP.Collector/
RUN dotnet restore src/FollowAlpha.LP.Collector/FollowAlpha.LP.Collector.csproj

# Copy the rest of the source and publish a self-contained-framework build.
COPY src/ src/
RUN dotnet publish src/FollowAlpha.LP.Collector/FollowAlpha.LP.Collector.csproj \
    -c Release -o /app/publish --no-restore -p:UseAppHost=false

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# The DB and the watchlist/wallets config are mounted at runtime (DEPLOYMENT.md). data/ is the persistent
# volume that holds the irrecoverable tick history; config/ holds wallets.json (mount read-only).
ENV LP_DB_PATH=/app/data/followalpha-lp.db \
    DOTNET_EnableDiagnostics=0
RUN mkdir -p /app/data /app/config

COPY --from=build /app/publish ./

# Run as the non-root user shipped in the aspnet image; it owns the data volume mount.
RUN chown -R app:app /app
USER app
VOLUME ["/app/data"]
EXPOSE 8080

ENTRYPOINT ["dotnet", "FollowAlpha.LP.Collector.dll"]
