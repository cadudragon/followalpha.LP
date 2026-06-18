# FollowAlpha.LP — Open Decisions & Intentional Deferrals

This file is the persistent parking lot for decisions and deferrals that must not live only in
commit bodies. It is not a task queue; `docs/CHECKLIST.md` remains the execution source of truth.

## How to Use This File

- **Intentional deferral** means "known and accepted, not a bug in the current item."
- **Operational requirement** means a later item must implement or prove it before its gate passes.
- **Analyst-review pending** means the implementation is allowed to proceed mechanically, but the
  modelling choice remains flagged for skeptical review before value claims are made.
- When an item is resolved, move it to the resolved section with the commit or tag that closed it.

## Intentional Deferrals

### Working-State CRUD

**Status:** deferred to the use cases that need it.

Phase 2.1 created the working-state tables (`Chain`, `DexProtocol`, `Asset`, `Pool`, `Wallet`,
`AlertRule`, `AppSetting`) but did not create CRUD repository ports for all of them. This is
intentional: the first Phase 2 value path needs append-only facts and descriptors first. CRUD seams
should be added when watchlist, wallet config, alert rules, or settings are actually exercised.

### Backtest and Audit Stores

**Status:** deferred to Phases 3/4.

`BacktestRun` and `AuditReport` tables exist in the Phase 2.1 schema, but
`IBacktestRunStore` and `IAuditReportStore` are not introduced yet. Add them with the replay and
audit use cases that write those outputs.

### Polymorphic Alert Targets

**Status:** deferred by design.

`AlertRule.TargetRef` is polymorphic (`Asset`, `Pool`, or `Position` depending on rule type), so
Phase 2.1 intentionally does not enforce a database foreign key for it. Validate target existence
in the alert-rule Application use case when alert CRUD is implemented.

### Query-First Insert-If-Absent

**Status:** accepted for Phase 2.

Fact ingestion uses provider-agnostic query-first insert-if-absent with unique natural keys as the
database defence. This assumes the DataSync worker is a single writer. If multi-writer ingestion is added,
replace or wrap this with provider-specific conflict handling behind the same ports.

### SQLite Decimal Storage

**Status:** accepted for Phase 2.

EF Core maps `decimal` to SQLite `TEXT` for precision. Phase 2.1 does not use decimal range filters
in persistence queries. If future use cases need numeric range filtering in SQLite, introduce a
purpose-specific projection or conversion rather than weakening stored precision.

### Asset View Chart Overlays

**Status:** deferred to Phase 6 (only after the Phase-5 value gate = GO).

`GET /v1/assets/{id}/chart` is staged (decided 2026-06-17, `API-CONTRACT.md`). Phase 3.2 delivers
`{ candles[], regimeTimeline[], rvVsPoolIv{} }`. The decorative Asset-View overlays — `emaOverlays`
(Skender), `structuralLevels`, `empiricalRangeBands` (would reuse `BandSurvivalEstimator`), and
`contextIndicators` — serve the chart UI, which does not exist before Phase 6 and may never exist (NO-GO).
Building them before the value gate is gold-plating a screen that has not earned its place (AGENTS rule 10).
Add them with the Asset View screen if Phase 5 is GO.

## Operational Requirements

### DataSync Semantics / Collector Naming Cleanup

**Status:** resolved in 3.2b — project renamed `FollowAlpha.LP.Collector` → `FollowAlpha.LP.DataSync`; docs reframed.

Decided 2026-06-18: the old `Collector` name and some always-on language overstated the worker's role.
FollowAlpha.LP is **not** building a price oracle and the local database is not the source of truth for
market prices. The worker's contract should be framed as **DataSync/backfill/cache/materialization**:
price data comes from external market-data providers/oracles and is persisted as cache/provenance for
reproducible reads; wallet events are RPC backfilled/synced; pool/tick data is provider/RPC/indexer-backed
sync/cache with source and freshness. If still cheap, rename `FollowAlpha.LP.Collector` to
`FollowAlpha.LP.DataSync`; otherwise at minimum update the docs and public language. This cleanup must not
change trading/range logic, introduce a new data provider, or synthetically fill tick-liquidity gaps.

### Always-On Oracle/VPS Deployment (deferred until after Phase 3 full)

**Status:** deploy-ready now; standing it up is deferred — `CHECKLIST.md` 2.6.

Decided 2026-06-17: the DataSync worker is *designed* always-on (tick-liquidity distributions cannot be
reconstructed retroactively), but the 24/7 Oracle/VPS deployment is **deferred until after Phase 3 full has
proven sufficient value/edge** — no paid 24/7 infrastructure before the engine earns it. `phase-2-done`
means the **agent gate is green and the host is deploy-ready** (Dockerfile + `docs/DEPLOYMENT.md`, image
builds and boots); it does **not** require a real cloud deploy, and the cloud deploy does **not** block the
start of Phase 3. Until the always-on deploy happens, **local/intermittent runs are acceptable** for smoke
and initial collection, with the **known, accepted loss of tick-liquidity during downtime — never
synthetically backfilled.**

### CI Evidence

**Status:** local gates are acceptable for item work; GitHub CI evidence is still the stronger gate.

Gate reports may cite local `dotnet build`, `dotnet test`, and `git diff --check`. When a gate
requires CI, record the GitHub Actions run or explicitly state that only local evidence was produced.

### Formal Receipts

**Status:** optional process layer unless explicitly requested.

Some reviews requested formal quality-gate / contract-verification receipts. The repository's
binding gate remains `docs/CHECKLIST.md` plus automated tests, unless a work order explicitly asks
for those receipt artifacts.

## Analyst-Review Pending

These modelling choices are declared before results and implemented mechanically, but they should
remain visible until the analyst/principal accepts them as adequate for value validation.

### Range Verdict Gate

**Current implementation:** `RangeVerdictCalculator` returns `OPEN` only when both gates pass:
net expectancy is at least the policy threshold and pool IV / forecast vol is at least the policy
threshold. Thresholds are supplied by `RangeVerdictPolicy`, not tuned in the Domain.

**Review focus:** whether the AND gate and IV-veto semantics are the right first v1 decision rule.

### Channel State Machine

**Current implementation:** channel cycles open at/below base, close at/above top, break out below
base, halt below the no-reopen floor or after too many reopens without a full crossing. Decisions
use price levels and counters, never running PnL.

**Review focus:** whether the reopen counter, halt semantics, and fee input naming are adequate.
`feesPerStep` must mean fees already attributable to the simulated position, not raw pool fees or
volume-derived gross fees.

### Estimator Modelling Choices

**Current implementation:** realized vol is sample standard deviation of log returns; trendiness is
path-efficiency; band survival uses relative arithmetic bands with overlapping windows and
right-censor count; Kaplan-Meier is deferred.

**Review focus:** whether these choices discriminate useful LP setups on real data before any value
claims are made.

### Regime Classification Thresholds

**Introduced:** Phase 3.2.

Regime (`RANGE`/`TRENDING`/`TRANSITION`) is a **new pure Domain component** — `RegimeClassifier` composing
the Phase-1 `RealizedVolEstimator` + `TrendinessEstimator`, with a `RegimePolicy` carrying the thresholds
(RV-percentile bands and a trendiness cutoff), mirroring `RangeVerdictCalculator`/`RangeVerdictPolicy`.
Thresholds are **declared, not tuned** against historical outcomes (RN-14 discipline) and the classifier
**never emits direction** (RN-07) — it reports the regime label plus its numeric evidence (RV percentile,
trendiness, windows).

**Review focus:** whether the percentile bands / trendiness cutoff and the minimum-history requirement for
a non-`422` answer discriminate useful regimes on real data before any value claim is made.

### Competing-Liquidity Definition

**Introduced:** Phase 3.2 (pool table + pool detail).

`competingLiquidity` is an **object, never a bare number** (a single "competing liquidity" figure would fake
precision). All liquidity figures are **raw L** (not USD), named with a `Raw` suffix so nobody reads them as
dollars:
- `activeLiquidityAtCurrentTickRaw` = `PoolSnapshot.Liquidity` (the snapshot's active L, exactly).
- `liquidityDensityAroundPriceRaw` = summed **gross** liquidity over `TickLiquiditySnapshot` ticks within the
  band `[tickLower, tickUpper]`.
- `bandPct` = frozen policy value (declared, not tuned); `tickLower`/`tickUpper` = the band's initialized tick
  bounds (`±bandPct` of the current price, snapped to tick spacing); `asOfUtc` = snapshot time.

It is descriptive crowding context, not a recommendation.

**Review focus:** whether the band width and the gross-vs-net choice give a meaningful crowding signal on
real pools.

### Pool IV Basis

**Introduced:** Phase 3.2.

`poolIv` is an object `{ annualized, basis:"pool_tvl_total", volumeUsd, tvlUsd, asOfUtc }`, not a bare number.
`annualized` is the Domain formula `2·fee·√(dayVolume/TVL)·√365` with the denominator = the pool's **total**
TVL (`pool_tvl_total`), with the inputs echoed for reproducibility; an
approximation that tends to understate IV for concentrated pools (active/in-range TVL would be more correct
but is not reconstructable from the current snapshot). 3.2 surfaces the number **with its basis** and draws
no cheap/expensive conclusion — that framing waits until the basis is sound and the value gate is reached.

**Review focus:** whether `pool_tvl_total` is an acceptable v1 basis, or whether an in-range-TVL basis is
needed before any IV-vs-RV claim.

### Snapshot Staleness Policy

**Introduced:** Phase 3.2.

A snapshot/tick distribution older than the freshness policy (default `2 ×` the DataSync pool-snapshot
cadence) is **not a current signal**. Single-resource endpoints (`/regime`, `/pools/{poolId}`) return `422`;
list endpoints flag the row (`dataStatus`) and null the derived metrics. A stale value is never presented as
current (RN-08 traceability; do not let old data masquerade as a live read).

**Review focus:** whether `2 ×` cadence is the right staleness bound once real collection cadence is known.

## Resolved

### Phase 2.1 Persistence Port Shape

**Resolved in:** Phase 2.1 commits ending at `dd27873`.

Persistence ports are grouped by write semantics:

- `IPriceStore`, `ISnapshotStore`, `IPositionEventStore`, `IIntentRecordStore`, `IDecisionLog` are
  append-only insert/append + query ports.
- `IPositionStore` owns the rebuildable `Position` projection and is allowed to upsert.

This is recorded in `docs/ARCHITECTURE.md` §5.

### Phase 2.1 DATA-MODEL Relationships

**Resolved in:** commit `3333c34`.

The EF model now enforces the DATA-MODEL relationships with foreign keys, while keeping
`AlertRule.TargetRef` polymorphic by design.

### Chain Event Reader Enrichment

**Resolved in:** Phase 2.4 (`collector: add phase 2.4 scheduled ingestion host`).

`SyncWalletPositionEvents` enriches the raw `ChainPositionEvent` into the persistable `PositionEvent`:
`positions(tokenId)` for ticks + token pair, `factory.getPool` for `PoolId`, ERC-20 `decimals` for human
amounts. The transferred-between-wallets edge case is handled by **owner-at-time attribution** —
`WalletPositionOwnership` intervals built from `Transfer` in/out at `(block, logIndex)` granularity; an event
is written only when it falls inside an open interval (ambiguous cases skipped + logged, never guessed).
`Collect.recipient ≠ owner` and positions opened before `fromBlock` are covered by the same windowing. Gas→USD
stays deferred (`GasCostUsd` nullable) until a reliable historical price source lands — never zero/guess.

### Chain Event Reader Wire-Decode Fixtures

**Resolved in:** Phase 2.3/2.4 (`RecordedRpcReplayTests`).

A **real chain-recorded JSON-RPC capture** of an Arbitrum position
(`tests/.../Recorded/Fixtures/arbitrum_position_5531934.json`) is replayed at the Nethereum transport level
through the production `NethereumEvmRpc`, so the real `eth_getLogs`/`positions`/`getPool`/`decimals`/receipt
wiring is validated offline (no key/network). An env-gated smoke test re-records it against the live public
RPC.

### SQLite Foreign Keys in Runtime

**Resolved in:** Phase 2.4.

The DataSync composition root wires the connection string with `Foreign Keys=True`
(`src/FollowAlpha.LP.DataSync/Program.cs`), enforcing the DATA-MODEL foreign keys at runtime.

### Phase 2 Completion Tag

**Resolved in:** Phase 2.5 (decision 2026-06-17).

`phase-2-done` = agent gate green + deploy-ready (Dockerfile + runbook, image builds/boots). It does **not**
depend on a real cloud deploy; the always-on Oracle/VPS deploy is the deferred operational follow-up above
(`CHECKLIST.md` 2.6). The squashed `InitialCreate` migration was a pre-first-deploy/tag clean-up; **from
`phase-2-done` onward, migrations are immutable** (schema changes are new incremental migrations).
