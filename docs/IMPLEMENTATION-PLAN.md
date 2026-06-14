# FollowAlpha.LP — Implementation Plan

Phases are strictly ordered. A phase is complete only when its **Definition of Done (DoD)** is fully met; do not start the next phase before that. Each phase ends with a commit tagged `phase-N-done`.

## Phase 0 — Skeleton & guardrails

Build the solution layout from `ARCHITECTURE.md` §3 with empty-but-compiling projects, CI (GitHub Actions: build + test), `.editorconfig`, analyzers, and the **architecture test project** asserting all dependency rules from day one.

**DoD:** `dotnet build` and `dotnet test` green in CI; architecture tests fail if anyone adds a forbidden reference (prove it with a temporary violation in a branch, then revert).

## Phase 1 — Domain kernel (the core that proves itself)

Implement `Domain` per `ARCHITECTURE.md` §4: primitives (tick/price/sqrtPrice conversions, fee tiers, BigInteger liquidity), the Elsts liquidity-math port, position model + valuation, intent benchmarks, IL computation, implied-vol formula, realized-vol and trendiness estimators, band survival estimator, fee-share estimator, range verdict calculator, channel simulator.

**Golden tests:** the Python reference (vendored in `tools/oracle/reference/`) is the **oracle**: a fixture-generation script (Python, lives in `tools/oracle/`) runs the reference math over the registered cases (`test_1`, `test_2`, `example_1..3` + additional cases as needed) and writes `tests/.../Golden/fixtures.json`, committed. The C# kernel must match the oracle within documented tolerances. Python is never called from product code.

Domain must expose pure functions sufficient for the replay layer (UC-09): band-survival over a price series, fee share, IL/exit-cost path, channel simulation. These are the same functions the verdict uses — replay reuses them, it does not duplicate math.

**DoD:** golden tests green against oracle-generated fixtures; every public Domain member unit-tested; Domain has zero package references (BCL only); architecture tests confirm purity.

## Phase 2 — Data adapters & always-on Collector

Infrastructure per §6: The Graph gateway adapter (Uniswap v3 subgraphs on Arbitrum and Base), Nethereum event reader (position NFT mint/burn/collect for configured wallets), SQLite/EF Core stores (append-only semantics), DEX protocol registry with the two day-1 descriptors. Collector host with scheduled jobs: pool watchlist snapshots (state, day volume, **tick liquidity distribution**), price series refresh, wallet event sync. Deploy to the VPS.

**DoD (agent):** Collector builds and runs locally against real endpoints when env vars are provided; re-running any job is idempotent (proven by test); integration tests pass against recorded fixtures; no secret in the repo; deployment artifact delivered — Dockerfile + compose file (or systemd unit) + `docs/DEPLOYMENT.md` runbook (provision, env vars, health check, log access, update procedure).

**DoD (principal):** deploy to the VPS following the runbook; confirm snapshots accumulating for the watchlist on both chains. Deployment is a human step — agents do not receive VPS credentials.

## Phase 3 — Module 0: LP-Audit (first real answer to the principal)

`AuditWalletPositions` use case + CLI command: wallet addresses → per-position audit — fees collected (reconciled with on-chain `collect` events), IL realized, result vs HODL, vs 50/50, vs intent benchmark (intent assigned by the principal per position via a simple input file), gas costs. Output: deterministic report (JSON + readable markdown).

Registered audit target (principal's wallet, positions on Arbitrum and Base): see `config/wallets.json`.

**DoD:** audit runs end-to-end on the principal's real wallets; report reproducible (same inputs → byte-identical output); kernel-vs-onchain fee reconciliation differences explained or flagged; principal receives the report.

## Phase 4 — API, decision log, Modules 1 & 2

**First deliverable — `EstimateRangeApr` (`/ranges/estimate-apr`):** the "what APR would I get with this range?" calculation (kernel L from capital+range ÷ in-range competing liquidity from snapshots × volume × fee tier). Honest by construction: includes the user's own L in the denominator (self-dilution), reports both while-in-range APR and time-in-range-adjusted expectancy, and shows volume-window sensitivity (7d vs 30d) instead of a single seductive number. QA: cross-validate estimates against Metrix Finance for the same pools/ranges and document divergences. UI rule (FSD): the fee side is never displayed without the IL side; the standalone calc exists only as an API/CLI building block.

Then the rest: API host with `/audit`, `/regime`, `/ranges/evaluate`, `/decisions` endpoints (OpenAPI); `ClassifyVolRegime` (Module 1) and `EvaluateRange` (Module 2: fee APR expected vs IL expected + IV vs forecast RV → OPEN/DON'T OPEN). Every verdict appended to the decision log with full inputs + content hash.

**Historical replay (UC-09, category A) ships alongside Module 2** via `/ranges/backtest` (and CLI): `BacktestBandSurvival`, `ReconcileFeeAprEstimateVsRealized`, `AnalyzeIvVsRvOutcome` — deterministic, no optimizer (RN-14). This is what makes the verdict's inputs falsifiable rather than opinionated; without it the verdict is just assertion.

**DoD:** evaluating a range via API persists an immutable decision-log entry retrievable via `/decisions`; regime and verdict outputs covered by use-case tests; at least one historical report exists showing band survival, estimated-vs-realized fee APR where data allows, and 7d/30d sensitivity; replay contains no parameter search or threshold tuning (RN-14); OpenAPI spec committed.

## Phase 5 — Frontend & Module 3

Next.js frontend (audit dashboard, regime panel, range evaluator screen, decision-log review) consuming the generated OpenAPI client; `ChannelSimulator` exposed via `/channels/simulate` with mandatory `ChannelPolicy` (max reopens, no-reopen floor, capital cap) — the API rejects simulations without a complete breakout protocol.

**DoD:** principal can run an audit, read a regime, get an OPEN/DON'T OPEN verdict and simulate a channel entirely from the browser; channel results always display the full series including breakouts (never a filtered "good stretch"); dashboard shows the post-OPEN monitor (fees vs IL race per open position, FSD UC-08).

## Phase 6 — Alerts & verdict-premise drift

`EvaluateAlertRules` in the Collector + `INotificationChannel` adapter (mechanism chosen with the principal: Telegram bot, e-mail or push); post-OPEN monitor flags when verdict premises changed (regime flip, pool IV below forecast RV), per FSD UC-07/UC-08.

**DoD:** a real alert rule fires end-to-end to the configured channel; premise-drift flags visible on the dashboard with the before/after numbers; alerts inform only — no execution paths exist.

## Backlog (post-Phase 6, unscheduled)

- Fork descriptors (Camelot, Aerodrome) via `IDexProtocolRegistry`.
- Postgres swap; real identity/multi-tenant (SaaS gate — principal's decision).
- Dune adapter as alternative data source.
- Decision-log forward-test report (verdicts vs subsequent outcomes — the tool auditing itself).
