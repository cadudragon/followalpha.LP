# FollowAlpha.LP — Build Checklist (clean-context work orders)

Operational queue for implementation. Each item is a **self-contained work order** sized for one agent session with a fresh (cleared) context. `IMPLEMENTATION-PLAN.md` holds the rationale and the full gate definitions; this file is the tick-list that drives execution.

## Clean-context protocol (read this every session)

1. Open `AGENTS.md`, then the **Read** list of the item you're taking. That is enough — do not rely on prior conversation.
2. Take the **first unchecked `[ ]` item whose Precondition tag exists in git**. Do only that item.
3. Pass its **Gate**. Then: commit the work, tick the box (`[x]`) in this file in the same commit, and create the item's tag if it has one.
4. Write a 3-line Gate Report (built / evidence / known gaps) in the commit body. **Stop. Clear context.** The next session takes the next item.
5. Judgment gates (5.2, 8.x) are **not** self-certified: the agent assembles evidence and stops; the analyst adjudicates and the principal decides.

Rule: never tick a box you didn't prove. The checkbox state in git is the project's source of truth for progress.

---

## Phase 0 — Skeleton & guardrails  · tag `phase-0-done`
Read: `ARCHITECTURE.md` §3, §10.
- [x] **0.1** Solution `.slnx` + the empty-but-compiling projects (6 src + 3 tests; the 4th test project, `Architecture.Tests`, is added in 0.2 → 10 total) per §3; `.editorconfig`, nullable on, warnings-as-errors, analyzers. Gate: `dotnet build` green.
- [x] **0.2** CI (GitHub Actions: build + test on push) + architecture-test project (NetArchTest) asserting dependency direction and Domain purity. Gate: CI green; a temporary forbidden reference makes arch tests fail (show it, revert). Tag `phase-0-done`.

## Phase 1 — Domain kernel  · precondition `phase-0-done` · tag `phase-1-done`
Read: `ARCHITECTURE.md` §4, `LP-KNOWLEDGE.md` §2-3 §6, `tools/oracle/README.md`.
- [x] **1.1** Primitives: `Tick`/`SqrtPriceX96` exact raw conversion, `HumanPrice`/`PoolPrice`/`TokenDecimals` scaling, `FeeTier`+tick spacing, `Liquidity`/`TokenAmount` (BigInteger/decimal), precision policy in one place. Follow `ARCHITECTURE.md` §4.1: only `HumanPrice + TokenDecimals -> PoolPrice -> Tick`, Uniswap floor semantics with +/-1 verified guard, explicit orientation, decimals here, and separate `PriceRange.ToInitializedTicks`. Gate: unit-tested, pure; tests cover `Tick <-> SqrtPriceX96` canonical constants/round-trips, `PoolPrice -> Tick` invariant, the +/-1 guard, scaling/orientation, range-containment (`lower<=requested<=upper`, `% tickSpacing == 0`), `TokenAmount` rounding, and Domain BCL-only.
- [x] **1.2** Liquidity-math kernel (port of `tools/oracle/reference/`) + the oracle fixture-generator script writing `tests/.../Golden/fixtures.json`. Gate: golden tests green within documented tolerances.
- [x] **1.3** Position model + valuation; intent benchmarks (HODL / 50-50 / scaled limit order); IL / exit-cost path. Gate: unit-tested against worked examples.
- [x] **1.4** Estimators (pure, data-as-input): realized vol, trendiness/path-efficiency, implied vol (`2·fee·√(vol/TVL)·√365`), band survival, fee share. Gate: unit-tested.
- [x] **1.5** `RangeVerdictCalculator` (→ `Open`/`DoNotOpen` + input snapshot) and `ChannelSimulator` (full series incl. breakouts). Gate: unit-tested; Domain still zero-package, purity tests green. Tag `phase-1-done`.

## Phase 2 — Data adapters & Collector  · precondition `phase-1-done` · tag `phase-2-done`
Read: `ARCHITECTURE.md` §6-7, `DATA-MODEL.md`, `TECH-STACK.md` §2, `NFR.md` §3-4.
Also read: `OPEN-DECISIONS.md` for accepted deferrals, operational requirements, and analyst-review flags that must survive context resets.
- [x] **2.1** EF Core + SQLite stores realizing `DATA-MODEL.md`; migrations; append-only repositories (insert+query only) for facts/decision/intent. Gate: append-only enforced and tested; idempotent insert-if-absent on natural keys.
- [x] **2.2** The Graph gateway adapter (`IPoolDataSource`): pool state, day volume, tick liquidity distribution; Uni v3 on Arbitrum + Base descriptors. Gate: integration test vs recorded fixtures; subgraph IDs recorded in PR.
- [x] **2.3** Nethereum event reader (`IChainEventReader`): mint/burn/collect + gas for configured wallets. Gate: integration test vs recorded fixtures.
- [x] **2.4** Collector host (Worker + Cronos): scheduled pool/tick snapshots, price refresh, wallet sync (owner-at-time attribution + incremental cursor/chunking); `/health` freshness. Gate: jobs idempotent (test); runs locally with env vars; no secret in repo.
- [x] **2.5** Deployment artifact (Dockerfile) + `docs/DEPLOYMENT.md` runbook. Gate (agent): runbook complete; image builds and boots locally. Tag `phase-2-done`. **`phase-2-done` = agent gate green + deploy-ready (Docker/runbook); it does NOT depend on a real cloud deploy.**
- [ ] **2.6** *(principal, human — operational follow-up, does NOT block Phase 3)* Always-on Oracle/VPS deploy via runbook; confirm snapshots accumulating on both chains. **Decided 2026-06-17: deferred until after Phase 3 full proves value/edge.** Until then, local/intermittent runs are acceptable for smoke and initial collection — with the known, accepted loss of tick-liquidity during downtime (never synthetically backfilled). Tick when the always-on deploy is actually done.

## Phase 3 — Range Advisor & descriptive replay (first value answer)  · precondition `phase-2-done` · tag `phase-3-done`
Read: `API-CONTRACT.md`, `FSD` UC-02/03/09, `TECH-STACK.md` §1, `NFR.md` §1-2.
- [x] **3.1** API host skeleton + `X-Api-Key` auth + OpenAPI + RFC7807 errors (incl. `422` insufficient-data). Gate: health endpoint + auth tested.
- [x] **3.2** Asset/pool exploration: `/assets`, `/assets/{id}/chart`, `/assets/{id}/regime`, `/assets/{id}/pools`, `/pools/{poolId}`. Gate: use-case tested; regime never emits direction (RN-07); pool table exposes fee tier, volume/TVL, IV, and competing liquidity. **Scope notes (2026-06-17):** (a) `/chart` is staged — 3.2 delivers `{ candles[], regimeTimeline[], rvVsPoolIv{} }`; the decorative Asset-View overlays (`emaOverlays`/`structuralLevels`/`empiricalRangeBands`/`contextIndicators`) are deferred to Phase 6 per `API-CONTRACT.md`. (b) Regime classification is a **new pure Domain component** (`RegimeClassifier` + `RegimePolicy` mapping RV-percentile + trendiness → `RANGE`/`TRENDING`/`TRANSITION`), not just reuse of the Phase-1 estimators; its thresholds and the `competingLiquidity` definition are `analyst-review pending` (`OPEN-DECISIONS.md`).
- [ ] **3.3** `EstimateRangeApr` (`/ranges/estimate-apr`): self-dilution, while-in-range vs time-adjusted, 7d/30d sensitivity. Gate: use-case tested; cross-check vs Metrix documented.
- [ ] **3.4** `SuggestRangeCandidates` (`/ranges/candidates`): deterministic predeclared band grid ranked by IV-vs-RV, band survival, expected fees, IL, and intent fit. Gate: tested; no optimizer/threshold-tuning; candidate reasons are included.
- [ ] **3.5** `EvaluateRange` (`/ranges/evaluate`) → verdict + inputs; appends immutable `DecisionLogEntry` (hash) every call; `/decisions` read. Gate: log immutable+retrievable (tested); `422` on thin data.
- [ ] **3.6** Replay UC-09 cat. A (`/ranges/backtest`): band survival, IV-vs-RV, fee-APR reconciliation. Gate: deterministic; **no optimizer/threshold-tuning** (code review + test, RN-14); refuses on thin data.
- [ ] **3.7** OpenAPI committed and matching `API-CONTRACT.md`; CLI covers the asset→pool→candidate ranges→verdict path. Gate: spec diff reviewed; end-to-end Range Advisor smoke on real watchlist data or recorded fixtures. Tag `phase-3-done`. **No frontend exists yet.**

## Phase 4 — LP-Audit, channel simulator, integrated headless product  · precondition `phase-3-done` · tag `phase-4-done`
Read: `FSD` UC-01/04, `DATA-MODEL.md` (Position/PositionEvent/IntentRecord/AuditReport), `LP-KNOWLEDGE.md` §3.
- [ ] **4.1** `AuditWalletPositions` use case + CLI/API: reconstruct positions from events, fees reconciled with `COLLECT`, IL/gas, vs HODL/50-50/intent benchmark (both benchmarks if reclassified). Deterministic JSON+markdown report. Gate: runs on `config/wallets.json` real wallet; byte-identical on re-run; reconciliation diffs flagged; refuses on insufficient data.
- [ ] **4.2** `SimulateChannel` (`/channels/simulate`): full series incl. breakouts; rejects incomplete breakout protocol (RN-04). Gate: tested.
- [ ] **4.3** Integrated headless smoke: Range Advisor + replay + audit + channel + decision log reachable via API/CLI. Gate: deterministic outputs, `422` on thin data, no frontend code. Tag `phase-4-done`.

## Phase 5 — VALUE VALIDATION GATE (GO/NO-GO)  · precondition `phase-4-done`
Read: `IMPLEMENTATION-PLAN.md` Phase 5, `LP-KNOWLEDGE.md` §6.
- [ ] **5.1** *(agent)* Assemble the **Edge Evidence Dossier** on real data: Range Advisor usefulness on watchlist pools, mechanism validation (band survival & IV-vs-RV discrimination), estimate fidelity vs realized & Metrix, audit retrospective, verdict sanity (descriptive, not edge proof). Stop. Gate: dossier complete and honest (no in-sample edge claims).
- [ ] **5.2** *(analyst + principal)* Analyst adjudicates the dossier; principal decides. Tick `phase-5-go` (→ Phase 6 authorized) or record `phase-5-nogo` with reasons (→ rethink fundamentals, no UI).

## Phase 6 — Frontend  · precondition `phase-5-go` · tag `phase-6-done`
Read: `FSD` §5 (Telas), `TECH-STACK.md` §3, `API-CONTRACT.md`.
- [ ] **6.1** Next.js scaffold + generated OpenAPI client + Tailwind/shadcn shell. Gate: lint/build CI job green.
- [ ] **6.2** Asset-first flow: Watchlist/Asset View (UC-02) → pools (Tela 3) → range evaluator (Tela 4, inputs always shown). Gate: end-to-end against API.
- [ ] **6.3** Audit dashboard (Tela 6) + decision log (Tela 7) + channel simulator (Tela 5, full series). Gate: end-to-end.
- [ ] **6.4** Dashboard + post-OPEN monitor (Tela 1, fees-vs-IL race, premise drift). Gate: end-to-end. Tag `phase-6-done`.

## Phase 7 — Alerts & drift  · precondition `phase-6-done` · tag `phase-7-done`
Read: `FSD` UC-07/UC-08, `ARCHITECTURE.md` (INotificationChannel).
- [ ] **7.1** `EvaluateAlertRules` in Collector + `INotificationChannel` adapter (channel chosen with principal) + alert-rule CRUD. Gate: a real alert fires end-to-end; inform-only (no execution path). Tag `phase-7-done`.

## Phase 8 — Forward-tracking  · precondition `phase-6-done` · recurring
Read: `IMPLEMENTATION-PLAN.md` Phase 8, `LP-KNOWLEDGE.md` §6.1.
- [ ] **8.1** Decision-log forward-test report (logged verdicts vs realized outcomes), pre-registered review interval. Gate (judgment): analyst reviews edge on **new** data; no in-sample threshold changes; rule changes pre-registered. Recurring — not ticked once.

---

## Backlog (unscheduled)
- [ ] Fork descriptors (Camelot, Aerodrome) via `IDexProtocolRegistry`.
- [ ] Postgres swap; identity/multi-tenant (SaaS gate).
- [ ] Dune adapter.
- [ ] Web screen for UC-09 replay (headless/API-first before Phase 5; web only after GO).
