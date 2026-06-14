# FollowAlpha.LP — Non-Functional Requirements

Authored 2026-06-14. Measurable targets the system must meet. Calibrated to **single-user today, SaaS-shaped later** — targets are deliberately modest where the workload is a human decision (seconds, not microseconds), and strict where correctness/auditability is the product's reason to exist. Each NFR is testable; "verify at implementation" items get a number in the PR that introduces them.

## 1. Determinism & reproducibility (the product's spine — strictest)

- **D1**: any report or verdict is a pure function of its inputs — same inputs → byte-identical output. Audit reports, backtest runs, and verdict `contentHash` must reproduce exactly. (Tested: re-run produces identical hash.)
- **D2**: Domain math is deterministic — no wall-clock, no RNG, no ambient state (enforced by architecture tests).
- **D3**: golden tests against the vendored oracle stay green at all times; tolerances are documented per case and never loosened to pass.

## 2. Performance (human-decision scale)

- **P1**: `POST /ranges/evaluate` returns in **< 3 s** p95 on warm collected data (no live network on the hot path — reads from SQLite snapshots).
- **P2**: `GET /assets/{id}/chart` returns in **< 2 s** p95 on warm data.
- **P3**: `POST /ranges/backtest` (descriptive replay over ≤ 2y daily data) in **< 10 s** p95.
- **P4**: `POST /audit/run` for one wallet (≤ a few hundred positions) completes in **< 60 s**; may be async with status polling.
- Rationale: these are analyst decisions, not HFT. Correctness and clarity beat latency.

## 3. Availability & data freshness

- **A1**: the Collector targets **24/7** on the VPS. Tick-liquidity snapshots are the only data that cannot be reconstructed retroactively — a missed tick snapshot is permanent loss; missed price/event syncs are recoverable by backfill.
- **A2**: snapshot cadence per watchlist pool is configurable; default target freshness **≤ 1 h** for pool/tick snapshots, **≤ 15 min** for price bars (verify against source rate limits).
- **A3**: `GET /health` exposes per-pool last-snapshot age; staleness beyond 2× cadence is flagged.
- **A4**: the API and frontend are single-instance local; no HA requirement in v1.

## 4. Data integrity, retention & growth

- **I1**: facts and decision records are append-only and idempotent (DATA-MODEL §4); enforced at the repository interface and tested.
- **I2**: every displayed number is traceable to its inputs (RN-08); the decision log carries the full input snapshot + hash.
- **I3**: growth driver is `TickLiquiditySnapshot` ≈ pools × snapshots/day × ticks-in-range. With a watchlist of ~25 pools at hourly cadence this is bounded to low-GB/year on SQLite — acceptable for v1. Revisit rollup/archival only when real volume is measured (no premature optimization).
- **I4**: input data that feeds a published report is hashed (`InputDataHash`); reports cite the hash.

## 5. Security & privacy

- **S1**: read-only on-chain. The codebase contains **no signing code and stores no private keys** (RN-05); a code-scan check in CI fails on signing-library imports or key-like patterns.
- **S2**: secrets only via environment/user-secrets (`GRAPH_API_KEY`, `ALCHEMY_API_KEY`, `RPC_URL_*`, `LP_API_KEY`); `.env*` gitignored; no secret in repo (CI secret-scan).
- **S3**: API requires `X-Api-Key`; unauthenticated requests get 401.
- **S4**: data resides on the principal's infrastructure; the only external calls are public queries (The Graph, GeckoTerminal, Coinbase, RPC reads). No user data sent to third parties.
- **S5** (SaaS gate, not v1): real identity, per-tenant isolation, rate limiting — deferred behind the `TenantId` seam.

## 6. Observability & operability

- **O1**: structured logging (Serilog) across hosts; Domain never logs.
- **O2**: Collector emits per-job outcome (rows ingested, duration, source) and freshness metrics surfaced by `/health`.
- **O3**: a Phase-2 deliverable `docs/DEPLOYMENT.md` runbook covers provision, env vars, health check, log access, update procedure.

## 7. Maintainability & quality

- **M1**: nullable enabled, warnings-as-errors, analyzers on; build + all tests green gate every commit.
- **M2**: architecture tests (NetArchTest) enforce dependency direction and Domain purity; a forbidden reference fails CI.
- **M3**: Domain has zero third-party package references (BCL only).
- **M4**: every public Domain member is unit-tested; use cases tested with port fakes; infrastructure tested against recorded fixtures (no live-network unit tests).

## 8. Portability

- **PT1**: a fresh clone builds and tests with only the .NET 10 SDK + env vars — no dependency on any path outside the repo (the oracle reference is vendored in `tools/oracle/reference/`).
- **PT2**: persistence swap SQLite → Postgres is behind repositories; no provider-specific SQL outside the adapter (SaaS gate).

## 9. Scope envelope (v1)

Sized for: 1 tenant, ≤ ~5 wallets, ≤ ~25 watchlist pools, 2 chains (Arbitrum, Base), daily + intraday bars. Beyond this is the SaaS gate, decided by the principal with real usage data.
