# FollowAlpha.LP — Architecture Contract

Authored 2026-06-12 by the project architect. This is a binding contract for implementation, not a suggestion. Changes require the architect's sign-off recorded in this file's history.

## 1. What this system is

A decision-support tool for concentrated-liquidity LPing (Program 3 charter, see `LP-KNOWLEDGE.md`): it classifies volatility regimes, compares pools, prices ranges (fees expected vs IL expected, pool-implied vol vs forecast vol) and renders auditable **OPEN / DON'T OPEN** verdicts, replays range/channel behavior historically, and audits past LP positions as calibration. It **recommends; the human executes**. Read-only on-chain.

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
- `Tick`, `SqrtPriceX96`, `HumanPrice`, `PoolPrice`, `TokenDecimals`, `FeeTier` (with tick spacing map), `TokenAmount`, `Liquidity` (BigInteger-backed).
- All raw-integer <-> decimal conversion is concentrated here, with documented precision policy: exact raw on-chain math for `Tick <-> SqrtPriceX96`; analytics-grade `decimal` for human-scale price views; tolerances are encoded in tests.

**Human price, raw pool price, and tick conversion (decided 2026-06-15 - binding).** Human prices and raw pool prices are distinct types (C-lite): `HumanPrice(value, orientation)`, `PoolPrice(rawToken1PerToken0)`, `TokenDecimals(token0, token1)`. The only path to a tick is `HumanPrice + TokenDecimals -> PoolPrice -> Tick/SqrtPriceX96`. This makes the footgun `new Price(2000).ToTick()` (no decimals) unrepresentable. Scaling rule: `P_raw = P_human(token1/token0) * 10^(dec1 - dec0)` (and invert first if the human price is token0/token1). Example USDC(dec0=6)/WETH(dec1=18): human `0.0005` (WETH per USDC) -> raw `0.0005 * 10^12 = 5e8`.

`PoolPrice -> Tick` is the only decimal-to-tick quantization. It follows Uniswap v3 `TickMath.getTickAtSqrtRatio` semantics: the **greatest tick whose raw pool price <= the given raw pool price** (floor in tick space), with invariant `Tick.ToPoolPrice(tick) <= poolPrice < Tick.ToPoolPrice(tick+1)` inside the supported analytics decimal window. Hardening rules:

1. **Verified rounding, not raw float.** Do not return `floor(log(price)/log(1.0001))` directly; floating error near a boundary returns the wrong integer. Compute the candidate, then verify the invariant and correct by +/-1 (the Uniswap guard step). Determinism (NFR D1) depends on this.
2. **Orientation is explicit.** `HumanPrice` carries which direction it means. Inversion relative to the user's mental model swaps floor/ceiling and lower/upper; the primitive pins the orientation, and range construction is defined in the user's price space and mapped explicitly.
3. **Decimals live here.** Human price <-> raw pool price includes token-decimal scaling; it is part of this single precision-policy location, not scattered.

**`Tick <-> SqrtPriceX96` is exact raw on-chain math.** It is an integer port of Uniswap v3-core `TickMath` (`GetSqrtRatioAtTick` with the round-up downcast; `GetTickAtSqrtRatio` as the greatest tick whose ratio <= input). `sqrtPriceX96` is raw on-chain data and must match Uniswap bit-for-bit, not be an analytics approximation. Validated against the published constants `getSqrtRatioAtTick(0)=2^96`, `MIN_SQRT_RATIO`, `MAX_SQRT_RATIO`.

**Raw vs analytics are separate.** `Tick` keeps the full Uniswap range `[-887272, 887272]` (raw on-chain fidelity). The analytics **decimal view** (`1.0001^tick`) is what is range-limited: a tick whose price falls outside the analytics-grade `decimal` window throws `PriceOutsideDecimalRangeException`, not a raw `OverflowException`. `Tick.ToSqrtPriceX96()` is exact; `Tick.ToPoolPrice()` / `PoolPrice.ToHumanPrice(...)` are analytics-grade decimal.

**Range-boundary conversion (separate operation).** For LP boundaries, `PriceRange.ToInitializedTicks(feeTier, decimals)`: lower bound rounds **down**, upper bound rounds **up**, both to the fee tier's initialized tick spacing. It **contains** the requested range, never silently narrows it. Invariants tested: the initialized lower tick's human price is at or below the requested lower human price, the initialized upper tick's human price is at or above the requested upper human price, `lowerTick % tickSpacing == 0`, `upperTick % tickSpacing == 0`. These concerns - canonical math, operational tick-spacing rounding, user-intent preservation - stay separate and are not mixed.

**Reference, not dependency.** `Nethereum.Uniswap` (cloned at `C:\SRC\reference_libs\Nethereum.Uniswap`) and the vendored v3-core `TickMath.sol` are used for cross-check / test vectors / future liquidity-math reference only. The Domain stays BCL-only; nothing from them is referenced at runtime. (Note: that clone's `V4TickMath` is scale-buggy: `GetSqrtRatioAtTick(0)` returns 2^64, so the authoritative source is the vendored canonical `TickMath.sol` plus its published `MIN/MAX_SQRT_RATIO`.)

**`TokenAmount` rejects negatives** (a future `TokenDelta` covers signed variation) and exposes **explicit rounding**: `FromDecimalExact` (throws if not representable at the given decimals), `FromDecimalFloor`, `FromDecimalRounded(mode)`; never a silent default. On-chain base units are not invented by hidden rounding.

**Domain BCL-only is asserted directly** (architecture test on `FollowAlpha.LP.Domain.csproj` containing no `PackageReference`), in addition to the namespace-dependency rules.

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

**Scaled-limit-order benchmark definition (decided 2026-06-15 — binding).** The benchmark is the *dry* (no-fee) scaled order you would otherwise place over the same range `[a,b]` with the **same single-sided capital** the position deposited (token1 for `Accumulate`, token0 for `Distribute`); the position's own average fill is the geometric mean `√(a·b)`. Two ladders are computed; partial fills are valued at the current price (the unfilled budget stays in its original token):
- **Primary — `UniformQuoteByPrice`**: equal quote per equally-spaced price level → continuous average fill = the **logarithmic mean** `(b−a)/ln(b/a)`. This is the official number for reports/verdicts ("did the LP beat the realistic dry ladder?").
- **Secondary — `UniformBaseByPrice`**: equal base (token0) per level → average fill = the **arithmetic mean** `(a+b)/2`. A sensitivity perspective only, not a substitute.
- Rejected: defining the benchmark as the AMM's own fee-less profile (edge would be fees only — not an independent alternative, contrary to `LP-KNOWLEDGE.md` §3/§6.2).

**Benchmark identity (decided 2026-06-15 — binding).** A benchmark's identity is a `BenchmarkSpec`, not a bare category: for `Hodl`/`FiftyFifty` the kind is enough, but for a limit order the identity is **kind + side + ladder** (`LimitOrder/Accumulate/UniformQuoteByPrice`, `…/Accumulate/UniformBaseByPrice`, `…/Distribute/UniformQuoteByPrice`, `…/Distribute/UniformBaseByPrice`). `IntentBenchmarks.For(intent)` returns the full specs (Harvest → Hodl + FiftyFifty; Accumulate/Distribute → their two limit-order specs, primary then secondary). `IntentBenchmarks.For(history)` is the union over distinct intents, **deduplicated by full spec** in first-seen order — so a reclassification `Accumulate → Distribute` preserves four distinct limit-order perspectives, never collapsing them to one.

### 4.4 Pricing & signals (pure functions; data arrives as inputs)
- `ImpliedVolCalculator`: `IV = 2·fee·sqrt(dailyVolume / tickTvl)·sqrt(365)`.
- `RealizedVolEstimator`, `TrendinessEstimator` (path-efficiency / ADX-like) — pure given a price series.
- `BandSurvivalEstimator`: empirical time-to-exit distribution of a band of width W given a historical series (pure given the series).
- `FeeShareEstimator`: expected fee APR given band, own L, in-range liquidity distribution, recent volume.
- `RangeVerdictCalculator`: combines the above into `Verdict { Open | DoNotOpen }` + full input snapshot (for the decision log).
- `ChannelSimulator`: given `ChannelPolicy` (range, reopen rules, max reopens, no-reopen floor, capital cap) and a price/fee series → full event/PnL series **including breakouts**.

**Estimator modelling choices (1.4 — declared before results, `LP-KNOWLEDGE.md` §6.1; thresholds are NOT set here, no tuning to results).**
- **Realized vol**: sample standard deviation (÷ n−1) of close-to-close **log returns**, annualized by `·sqrt(periodsPerYear)`.
- **Trendiness**: Kaufman **efficiency ratio** (net displacement ÷ path length, in [0,1]); direction is never emitted (RN-07). ADX is a deferred alternative.
- **Band survival**: a **relative arithmetic** band `[p·(1−W), p·(1+W)]` centred on each entry, **overlapping** windows (every entry is a start), exit = strictly outside (bounds inclusive); entries that never exit are **right-censored** and reported, quantiles are over observed exits (a Kaplan–Meier survival curve is deferred).
- **Fee share**: `ownL / inRangeL` (own is included → adding liquidity dilutes); the APR is **while-in-range** (the band-survival time adjustment is applied by the caller, not the estimator).
- All estimators **fail closed** on invalid market data: prices must be strictly positive, and the inputs are validated up front.

**Verdict & channel decisions (1.5 — declared before results; flagged for analyst review).**
- **`RangeVerdictCalculator`**: OPEN iff **both** gates pass — net expectancy (expected fees over the likely horizon − expected exit cost) ≥ `MinNetExpectancy`, **and** pool IV ÷ forecast vol ≥ `MinIvToForecastRatio` (the IV gate is a veto regardless of APR, §6b). Thresholds are a caller-supplied `RangeVerdictPolicy` (no magic constants, no tuning to results, §6.1); the full inputs + policy + derived metrics are returned as the decision-log snapshot.
- **`ChannelSimulator`**: each cycle is a single-sided LP that buys at the base and sells across the band, valued with the kernel (§4.2). CloseAtTop (price ≥ upper) is a full crossing and resets the reopen counter; BreakoutDown (price &lt; lower) is marked to market and closed; the channel halts when price is below the no-reopen floor or after `MaxReopensWithoutFullCrossing` opens without a crossing. All open/close/reopen/halt decisions are functions of price levels and counters, **never the running PnL** (§6.6). The result is the full ordered event/PnL series including every breakout and halt — never the cherry-picked good run (§5).

## 5. Application

Use cases (one class per operation, CQRS-lite, no MediatR needed):

- Module 1: `ClassifyVolRegime` (asset → RANGE/TRENDING/TRANSITION + evidence).
- Module 2 / Range Advisor: `CompareAssetPools`, `SuggestRangeCandidates` (deterministic predeclared band grid, no optimizer), `EstimateRangeApr`, `EvaluateRange` (pool, band, intent → verdict, persisted to decision log), `BacktestBandSurvival`, `ReconcileFeeAprEstimateVsRealized`, `AnalyzeIvVsRvOutcome`.
- Module 3: `SimulateChannel`, `EvaluateChannelPolicy`.
- Module 0 / calibration: `AuditWalletPositions` (wallet → per-position audit: fees collected vs IL vs HODL vs intent benchmark, costs included).
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
- **Api** (ASP.NET Core minimal APIs): REST + OpenAPI. Endpoints mirror use cases (`/assets`, `/regime`, `/ranges/evaluate`, `/ranges/backtest`, `/channels/simulate`, `/audit`, `/decisions`). Auth: API-key middleware seam (single key today; real identity later). CORS for the frontend origin.
- **Cli**: thin wrapper over the same use cases for headless operation and ops tasks (run Range Advisor flow, replay, audit, trigger snapshot, export decision log).
- **frontend/** (Next.js + TypeScript): dashboards — asset-first range evaluator (the OPEN/DON'T OPEN screen with its inputs), replay/backtest views after the value gate, channel simulator, audit report, decision-log review. Consumes the OpenAPI-generated client. Charts: lightweight-charts or recharts. **No business logic client-side** — the API is the single source of verdicts.

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
