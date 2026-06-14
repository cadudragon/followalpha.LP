# FollowAlpha.LP — Architecture Contract

Authored 2026-06-12 by the project architect. This is a binding contract for implementation, not a suggestion. Changes require the architect's sign-off recorded in this file's history.

## 1. What this system is

A decision-support tool for concentrated-liquidity LPing (Program 3 charter, see `LP-KNOWLEDGE.md`): it audits past LP positions, classifies volatility regimes, prices ranges (fees expected vs IL expected, pool-implied vol vs forecast vol) and renders auditable **OPEN / DON'T OPEN** verdicts, and simulates channel strategies with mandatory breakout protocols. It **recommends; the human executes**. Read-only on-chain.

Operational profile: single user today; potentially SaaS later. Always-on deployment (VPS) because tick-level liquidity distributions cannot be reconstructed retroactively — the collector snapshots them continuously from day 1.

## 2. Architectural style

**Modular monolith, ports & adapters (hexagonal), domain-centric.** One deployable backend, strict internal boundaries. "God tier" here means boundaries so clean that any module could be extracted into a service later without rewriting the core — not that we build distributed infrastructure now.

Explicit non-goals (now): microservices, message brokers, Kubernetes, multi-tenant billing/identity, autonomous execution, Dune dependency (The Graph + RPC suffice; Dune is a future optional adapter).

## 3. Solution layout

```
FollowAlpha.LP.slnx
src/
  FollowAlpha.LP.Domain/            # pure kernel — references NOTHING
  FollowAlpha.LP.Application/       # use cases + ports — references Domain
  FollowAlpha.LP.Infrastructure/    # adapters — references Application, Domain
  FollowAlpha.LP.Api/               # ASP.NET Core minimal API host
  FollowAlpha.LP.Collector/         # Worker Service host (scheduled snapshots)
  FollowAlpha.LP.Cli/               # thin CLI host (Phase 1-3 interface, ops)
frontend/                           # Next.js app (separate; consumes the API)
tests/
  FollowAlpha.LP.Domain.Tests/      # incl. Golden/ fixtures from the Elsts repo
  FollowAlpha.LP.Application.Tests/
  FollowAlpha.LP.Infrastructure.Tests/
  FollowAlpha.LP.Architecture.Tests/  # NetArchTest rules: dependency directions, Domain purity
docs/
```

Hosts contain no logic: composition root (DI), configuration binding, transport mapping. All behavior lives in Application/Domain.

## 4. Domain (the core that must prove itself first)

Pure, deterministic, immutable, fully unit-tested. Contents:

### 4.1 Primitives
- `Tick`, `SqrtPriceX96`, `Price` and the conversions between them (`price = 1.0001^tick`), `FeeTier` (with tick spacing map), `TokenAmount`, `Liquidity` (BigInteger-backed).
- All raw-integer ↔ decimal conversion concentrated here, with documented precision policy: analytics-grade `decimal` is acceptable; tolerances are encoded in golden tests.

### 4.2 Liquidity math kernel (validated against the Elsts reference)
- `LiquidityMath`: L from amounts+range (`get_liquidity_*`), amounts from L+price (`calculate_x/y`), range bounds from amounts (`calculate_a/b`), inventory deltas as price moves (whitepaper delta form). This is ~80 lines of arithmetic — it IS the core and is implemented in C#.
- **The Python reference app is an oracle and exploration tool, never a runtime dependency.** Principal's decision (2026-06-12): do not port the *app*. Division of labor:
  - The reference math (vendored in `tools/oracle/reference/`) is run standalone (CLI) to **generate golden fixtures**: a small script executes the Python math over the registered test cases and writes expected values to `tests/.../Golden/fixtures.json`, committed to the repo. The C# kernel must converge to the oracle, never the reverse.
  - Its subgraph scripts remain available as-is for manual exploration/ops. Product code never shells out to Python: a subprocess at the heart of the Domain would destroy determinism, testability and VPS deployment — the opposite of a god-standard core.
- Golden tests replicate the reference repo's `test_1`, `test_2`, `example_1..3` values (plus any oracle-generated cases) with stated tolerances. These tests are the kernel's acceptance contract.

### 4.3 Position model & valuation
- `RangePosition` (pool, range, L, opened-at), `PositionValuation` (x, y, value at price P), `HodlBenchmark`, `FiftyFiftyBenchmark`, `LimitOrderBenchmark` (scaled limit order without fees).
- `Intent` = `Accumulate | Distribute | Harvest` with its benchmark mapping (see `LP-KNOWLEDGE.md` §3). Intent records are immutable; reclassification **appends** a new intent record (dated, with reason) — the original is preserved, valuations after reclassification are computed against both intents' benchmarks, and the position is flagged in all reports.
- IL / LVR-style computations: position value vs each benchmark over a price path or at a point.

### 4.4 Pricing & signals (pure functions; data arrives as inputs)
- `ImpliedVolCalculator`: `IV = 2·fee·sqrt(dailyVolume / tickTvl)·sqrt(365)`.
- `RealizedVolEstimator`, `TrendinessEstimator` (path-efficiency / ADX-like) — pure given a price series.
- `BandSurvivalEstimator`: empirical time-to-exit distribution of a band of width W given a historical series (pure given the series).
- `FeeShareEstimator`: expected fee APR given band, own L, in-range liquidity distribution, recent volume.
- `RangeVerdictCalculator`: combines the above into `Verdict { Open | DoNotOpen }` + full input snapshot (for the decision log).
- `ChannelSimulator`: given `ChannelPolicy` (range, reopen rules, max reopens, no-reopen floor, capital cap) and a price/fee series → full event/PnL series **including breakouts**.

## 5. Application

Use cases (one class per operation, CQRS-lite, no MediatR needed):

- Module 0: `AuditWalletPositions` (wallet → per-position audit: fees collected vs IL vs HODL vs intent benchmark, costs included).
- Module 1: `ClassifyVolRegime` (asset → RANGE/TRENDING/TRANSITION + evidence).
- Module 2: `EvaluateRange` (pool, band, intent → verdict, persisted to decision log).
- Module 3: `SimulateChannel`, `EvaluateChannelPolicy`.
- Ingestion: `SnapshotPool`, `IngestPositionEvents`, `IngestPriceSeries` (called by Collector).

Ports (interfaces owned by Application):

- `IPoolDataSource` — pool state, day data, tick-level liquidity distribution (The Graph).
- `IChainEventReader` — position NFT events: mint/burn/collect (RPC).
- `IPriceSeriesSource` — spot daily/intraday series.
- `IDexProtocolRegistry` — descriptors for Uniswap v3 and forks per chain (subgraph id, NFT manager address, fee tiers). Adding a DEX/chain = adding a descriptor + (if needed) an adapter, nothing else changes.
- `ISnapshotStore`, `IPositionStore`, `IPriceStore` — persistence (append-only semantics for facts).
- `IDecisionLog` — append-only verdict log: every `EvaluateRange`/channel decision with full inputs + content hash. **This log is the product's own forward-test** — six months of logged verdicts tell us whether the tool has edge.
- `INotificationChannel` — alert delivery for UC-07 (mechanism configurable: Telegram/e-mail/push; implemented in Phase 6).
- `IClock`.

Monitoring use cases (UC-07/UC-08, FSD v1.1): `EvaluateAlertRules` (Collector-driven) and `MonitorOpenPositions` (fees vs IL race, verdict-premise drift flags — informational only, never an execution path).

Historical replay use cases (UC-09, FSD v1.1; decided 2026-06-14): a **thin, custom, LP-native** replay layer — no external backtesting engine in the runtime. Orchestration in Application; all math is the pure Domain kernel (band survival, fee share, IL path, channel simulation); historical data from the stores/ports. Use cases: `BacktestBandSurvival`, `ReconcileFeeAprEstimateVsRealized`, `AnalyzeIvVsRvOutcome`, `SimulateChannelPolicy`. These are **descriptive / input-calibration** (category A): measure the empirical distributions that feed the verdict and reconcile estimates against realized outcomes. **Deterministic, no optimizer** — no parameter search, no threshold tuning against historical outcomes (RN-14). Verdict-edge evaluation ("would OPEN have beaten DON'T OPEN") is category B and **out of v1**: measure verdict edge via the decision log auditing itself on new forward data, or a separate pre-registered walk-forward study — never in-sample. LEAN may be an *external* research tool for cross-checking pipelines, never a product dependency.

## 6. Infrastructure

- `TheGraphPoolDataSource`: The Graph **decentralized gateway** (API key via env; the legacy hosted-service endpoint is dead). Subgraph ids configured per `IDexProtocolRegistry` descriptor. Day-1 targets: Uniswap v3 on **Arbitrum** and **Base** (forks like Camelot/Aerodrome enter later as descriptors).
- `EvmRpcEventReader`: Nethereum against configurable RPC endpoints (Arbitrum, Base) for NonfungiblePositionManager events of the principal's wallets.
- Persistence: **SQLite + EF Core** (file DB, zero ops), migrations in repo. Repository implementations enforce append-only on snapshot/event/decision tables. Postgres is a future swap behind the same repositories — no SQLite-specific SQL outside the adapter.
- Resilience: HTTP retries with backoff (Polly), request/response fixtures recorded for integration tests (no live-network unit tests).

## 7. Hosts

- **Collector** (Worker Service, runs on the VPS): scheduled jobs — pool snapshots (state, day volume, tick liquidity distribution) for a configured watchlist, price series refresh, wallet event sync. Idempotent; health endpoint; structured logs (Serilog). Missing a run is recoverable for events/prices, NOT for tick distributions — hence always-on.
- **Api** (ASP.NET Core minimal APIs): REST + OpenAPI. Endpoints mirror use cases (`/audit`, `/regime`, `/ranges/evaluate`, `/channels/simulate`, `/decisions`). Auth: API-key middleware seam (single key today; real identity later). CORS for the frontend origin.
- **Cli**: thin wrapper over the same use cases for Phase 1-3 operation and ops tasks (run audit, trigger snapshot, export decision log).
- **frontend/** (Next.js + TypeScript): dashboards — audit report, regime panel, range evaluator (the OPEN/DON'T OPEN screen with its inputs), channel simulator, decision-log review. Consumes the OpenAPI-generated client. Charts: lightweight-charts or recharts. **No business logic client-side** — the API is the single source of verdicts.

## 8. Cross-cutting rules

- `TenantId` on every persisted aggregate (constant `default` tenant now). No other SaaS plumbing.
- Structured logging (Serilog) everywhere except Domain (which never logs).
- All times UTC; `DateTimeOffset` in storage.
- Configuration via `appsettings.json` + environment overrides; secrets only via environment/user-secrets.
- Architecture tests (NetArchTest) assert: dependency directions; Domain has no references; Domain types contain no `DateTime.Now`, no I/O namespaces.

## 9. Configuration contract

Required environment variables (names are part of the contract; values are never committed):

| Variable | Used by | Purpose |
|---|---|---|
| `GRAPH_API_KEY` | Infrastructure (TheGraph adapter) | The Graph decentralized gateway key |
| `RPC_URL_ARBITRUM` | Infrastructure (event reader) | Arbitrum One RPC endpoint |
| `RPC_URL_BASE` | Infrastructure (event reader) | Base RPC endpoint |
| `LP_DB_PATH` | Infrastructure (SQLite) | Database file path (default: `./data/followalpha-lp.db`) |
| `LP_API_KEY` | Api host | Single API key for the auth seam |

Subgraph IDs for Uniswap v3 on Arbitrum and Base are resolved at implementation time (they live on the decentralized network and may change), stored in the `IDexProtocolRegistry` descriptor configuration (`appsettings.json`), and documented in the PR that introduces them. A `.env.example` file with all variable names (no values) is committed.

## 10. Quality bar (enforced, not aspirational)

- Warnings as errors; nullable enabled; analyzers on.
- Domain: every public member exercised by tests; golden tests green at all times.
- Application: use-case tests with port fakes.
- Infrastructure: integration tests against recorded fixtures.
- CI (GitHub Actions): build + all tests on every push; frontend lint/build job.
