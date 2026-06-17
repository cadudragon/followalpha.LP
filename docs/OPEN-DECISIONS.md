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
database defence. This assumes the Collector is a single writer. If multi-writer ingestion is added,
replace or wrap this with provider-specific conflict handling behind the same ports.

### SQLite Decimal Storage

**Status:** accepted for Phase 2.

EF Core maps `decimal` to SQLite `TEXT` for precision. Phase 2.1 does not use decimal range filters
in persistence queries. If future use cases need numeric range filtering in SQLite, introduce a
purpose-specific projection or conversion rather than weakening stored precision.

### Phase 2 Completion Tag

**Status:** intentionally later.

`phase-2-done` is created only after item 2.5 passes the agent gate. Item 2.6 is the principal's
VPS deployment confirmation and is logged separately.

### Chain Event Reader Enrichment

**Status:** deferred to the Collector (2.4) / LP-Audit (4).

Phase 2.3's `IChainEventReader` is a thin raw reader (`ChainPositionEvent`: raw integers as text,
native gas; no tick/pool/decimals/USD). Building the persistable `PositionEvent` requires enrichment
done downstream: ownership/tokenId attribution via NPM `Transfer(to=wallet)`; `positions(tokenId)`
for `tickLower`/`tickUpper` (with the event's block tag when historical state matters);
`factory.getPool` (cross-checkable by CREATE2) for `PoolId`; token decimals for human
`Amount0`/`Amount1`; gas→USD via a price source; and writing the `PositionEvent` fact.

Edge cases to handle at enrichment (not in the reader):
- a `tokenId` transferred between wallets mid-life (attribution must be owner/time aware);
- `Collect.recipient` ≠ the position owner;
- positions opened before the queried `fromBlock`;
- `positions(tokenId)` current state may not equal the state at a historical block.

### Chain Event Reader Wire-Decode Fixtures

**Status:** decode exercised offline against ABI-correct logs; chain-recorded capture still ideal.

The `IEvmRpc` seam returns **raw** `FilterLog`s, so the reader's real Nethereum log→DTO decode runs in
the offline tests against ABI-correct representative logs (correct keccak topic0, indexed tokenId topic1,
ABI-encoded data — built by `NpmLogFactory`). This proves the event DTO attributes and the sign/amount/
recipient mapping. A chain-recorded JSON-RPC capture (real `eth_getLogs`/receipt/block from the gateway)
remains the ideal final fixture, pending an RPC key / network — same gold standard as the 2.2 The Graph
fixtures. The production `eth_getLogs` filter wiring (`NethereumEvmRpc`) is validated only by that real
capture, not by the offline suite.

## Operational Requirements

### SQLite Foreign Keys in Runtime

**Status:** must be handled in Phase 2.4 composition root.

Phase 2.1 enforces DATA-MODEL relationships with foreign keys and tests them against SQLite. The
runtime connection string must explicitly enable SQLite foreign keys:

```text
Foreign Keys=True
```

This belongs in the Collector/API composition root or configuration binding that wires `LP_DB_PATH`.

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
