# FollowAlpha.LP — Stack & Architecture Decisions (consolidated reference)

Authored 2026-06-13. Single-page consolidation of every backend/frontend/architecture decision made so far. **Source of truth for the why** is `ARCHITECTURE.md` (contract) and `TECH-STACK.md` (analysis layer); this file is the *what*, in one place, for quick reference and onboarding. If anything here conflicts with those two, they win.

Constraints that drove every choice: .NET 10; best-of-market **open-source or free tier only**; single user today, SaaS-shaped tomorrow; the tool **recommends, never executes** (read-only on-chain, no signing).

---

## 1. Architecture style (decided)

**Modular monolith, ports & adapters (hexagonal), domain-centric.** One deployable backend, strict internal boundaries enforced by architecture tests. Not microservices, no broker, no Kubernetes — "god tier" = clean boundaries, not distributed infra.

Dependency direction (enforced by `NetArchTest`):

```
Domain  ◄──  Application  ◄──  Infrastructure
  ▲              ▲                  ▲
  └── Api / DataSync / Cli (hosts: DI + transport only) ──┘
              frontend/  ──► talks only to Api (OpenAPI client)
```

- **Domain**: pure, deterministic, immutable, BCL-only. Liquidity math (Elsts port), intent accounting, verdict math, vol/trendiness/survival estimators. No I/O, no `DateTime.Now`, no logging.
- **Application**: use cases + ports (interfaces). Context indicators (Skender) live here.
- **Infrastructure**: adapters implementing the ports (The Graph, RPC, SQLite, notifications).
- **Hosts**: `Api`, `DataSync` (24/7 VPS), `Cli` — composition root only.

## 2. Solution layout (decided)

```
FollowAlpha.LP.slnx
src/
  FollowAlpha.LP.Domain/            # pure kernel — references NOTHING
  FollowAlpha.LP.Application/       # use cases + ports — refs Domain
  FollowAlpha.LP.Infrastructure/    # adapters — refs Application, Domain
  FollowAlpha.LP.Api/               # ASP.NET Core minimal API
  FollowAlpha.LP.DataSync/         # Worker Service (scheduled snapshots)
  FollowAlpha.LP.Cli/               # thin CLI (phases 1-3 + ops)
frontend/                           # Next.js app (separate; OpenAPI client)
tests/
  FollowAlpha.LP.Domain.Tests/      # + Golden/ fixtures from oracle
  FollowAlpha.LP.Application.Tests/
  FollowAlpha.LP.Infrastructure.Tests/
  FollowAlpha.LP.Architecture.Tests/  # dependency + purity rules
tools/oracle/                       # vendored Elsts Python = golden-fixture oracle
docs/  config/
```

## 3. Backend packages (decided)

| Package | Layer | Purpose | License |
|---|---|---|---|
| ASP.NET Core minimal APIs | Api | HTTP host, OpenAPI | MIT |
| Swashbuckle.AspNetCore | Api | OpenAPI/Swagger doc generation | MIT |
| Microsoft.EntityFrameworkCore.Sqlite | Infrastructure | persistence (Postgres seam later) | MIT |
| Nethereum | Infrastructure | EVM: event logs (mint/burn/collect), Chainlink reads | MIT |
| **Skender.Stock.Indicators** | Application | context indicators (EMA/SMA/ATR/ADX/RSI/BB) — **never in Domain** | Apache-2.0 |
| Cronos | DataSync | cron expression parsing for scheduled jobs | MIT |
| Microsoft.Extensions.Http.Resilience (Polly) | Infrastructure | retries/backoff on all data sources | MIT/BSD-3 |
| Serilog (+ Console/File sinks) | hosts | structured logging (never in Domain) | Apache-2.0 |
| System.Text.Json | all | serialization (BCL) | MIT |
| `HttpClient` (raw) | Infrastructure | The Graph / GeckoTerminal queries (static GraphQL strings, no GraphQL lib) | BCL |

**Domain has zero package references** — BCL only (`System.Numerics.BigInteger` for L/sqrtPriceX96, `decimal` for human-scale math). This is the rule that keeps the core provable.

### Test packages

| Package | Purpose | License |
|---|---|---|
| xUnit | test runner | Apache-2.0 |
| FluentAssertions | assertions | Apache-2.0 |
| NSubstitute | mocking (at ports only) | BSD |
| NetArchTest.Rules | dependency-direction + Domain-purity tests | MIT |

## 4. Frontend packages (decided)

| Package | Purpose | License |
|---|---|---|
| Next.js + TypeScript | framework (App Router) | MIT |
| **TradingView Lightweight Charts** | candles, overlays, range bands, channel viz | Apache-2.0 |
| Recharts | liquidity-depth histogram, fees-vs-IL bars, sparklines | MIT |
| openapi-typescript + openapi-fetch | API client generated from .NET OpenAPI (contract-first) | MIT |
| TanStack Query | server state, caching, polling (monitor/alerts) | MIT |
| Tailwind CSS + shadcn/ui | styling + UI components | MIT |

Rejected: TradingView Advanced Charts (closed-source, client-side indicators violate "indicators computed server-side"); D3 from scratch; any paid SDK.

## 5. Data sources (decided — each with primary + cross-check)

| Data | Primary | Cross-check | Tier |
|---|---|---|---|
| Pool state, volume, tick liquidity | The Graph gateway (Uni v3, Arbitrum+Base) | GeckoTerminal | free + API key |
| Pool OHLCV | GeckoTerminal | subgraph swaps | free, no key |
| Asset spot candles | Coinbase public (Program 1 pipeline) | Binance public | free |
| Spot price sanity | Chainlink via RPC read | CoinGecko | free |
| Position events / gas | Alchemy free tier RPC | public RPCs | free |

## 6. Persistence shape (decided)

- SQLite file DB; EF Core code-first migrations in repo.
- Three data natures: **facts** (snapshots, on-chain events) = append-only + idempotent re-ingest; **decision log + annotations** = append-only; **working state** (watchlist, intents, alert rules) = CRUD.
- `TenantId` on every persisted aggregate (constant `default` today; SaaS seam).
- Postgres is a future swap behind the repository ports — no SQLite-specific SQL leaks out of the adapter.

## 7. Cross-cutting (decided)

- nullable enabled, warnings-as-errors, analyzers on; build+test green before every commit.
- All times UTC (`DateTimeOffset` in storage).
- Secrets only via env/user-secrets (`GRAPH_API_KEY`, `ALCHEMY_API_KEY`, `RPC_URL_*`, `LP_DB_PATH`, `LP_API_KEY`, + analysis-layer vars in TECH-STACK §5). `.env*` gitignored.
- Domain language in English (range, fees, IL, intents ACCUMULATE/DISTRIBUTE/HARVEST, verdict OPEN/DON'T OPEN, regimes RANGE/TRENDING/TRANSITION); UI prose pt-BR.
- CI: GitHub Actions — build + all tests on push; frontend lint/build job.

## 7b. Backtesting (decided 2026-06-14, priority updated 2026-06-15 — NOT open)

v1 ships a **thin, custom, LP-native deterministic replay layer**, not a generic backtesting engine. Orchestration in Application, math in the Domain kernel, data from the stores. Purpose: calibrate/validate the verdict's inputs (band survival, IV-vs-RV outcomes, fee APR estimated-vs-realized) — category A. **No optimizer, no parameter/threshold search (RN-14).** On 2026-06-15 this replay was promoted into the first product-value path: Range Advisor must use it to explain candidate ranges/pools before LP-Audit is required. Candidate ranges come from a deterministic predeclared grid, not from fitting historical winners. Verdict-edge evaluation (category B) is out of v1 — answered by the decision log on new forward data or a separate pre-registered walk-forward study, never in-sample. LEAN allowed only as an external research cross-check, never a runtime dependency.

## 8. Still open (decided to defer, not forgotten)

- Notification channel mechanism for alerts (Telegram bot / e-mail / push) — chosen with principal at Phase 6.
- Exact subgraph IDs for Uni v3 on Arbitrum/Base — resolved at Phase 2, recorded in the PR.
- The list of RN-13 context indicators on the Asset View — analyst's recommended starting roster is in `TECH-STACK.md` ("Indicator roster for the Asset View"); final list locked against real collected data once the DataSync runs. (Decision math that feeds the verdict is already fixed in Domain — that part is NOT open.)
- Postgres + real identity/multi-tenant — SaaS gate, principal's decision.
