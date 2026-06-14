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
- [x] **0.1** Solution `.slnx` + the 9 empty-but-compiling projects (src + tests) per §3; `.editorconfig`, nullable on, warnings-as-errors, analyzers. Gate: `dotnet build` green.
- [x] **0.2** CI (GitHub Actions: build + test on push) + architecture-test project (NetArchTest) asserting dependency direction and Domain purity. Gate: CI green; a temporary forbidden reference makes arch tests fail (show it, revert). Tag `phase-0-done`.

## Phase 1 — Domain kernel  · precondition `phase-0-done` · tag `phase-1-done`
Read: `ARCHITECTURE.md` §4, `LP-KNOWLEDGE.md` §2-3 §6, `tools/oracle/README.md`.
- [x] **1.1** Primitives: `Tick`/`SqrtPriceX96`/`Price` conversions, `FeeTier`+tick spacing, `Liquidity`/`TokenAmount` (BigInteger/decimal), precision policy in one place. Follow the **Price→Tick convention** in `ARCHITECTURE.md` §4.1 (Uniswap floor semantics + ±1 verified guard, explicit orientation, decimals here) and the separate `PriceRange.ToInitializedTicks`. Gate: unit-tested, pure; tests cover the invariants `TickToPrice(t) <= price < TickToPrice(t+1)`, the ±1 guard at a boundary, an inversion/orientation case, and range-containment (`lower<=requested<=upper`, `% tickSpacing == 0`).
- [ ] **1.2** Liquidity-math kernel (port of `tools/oracle/reference/`) + the oracle fixture-generator script writing `tests/.../Golden/fixtures.json`. Gate: golden tests green within documented tolerances.
- [ ] **1.3** Position model + valuation; intent benchmarks (HODL / 50-50 / scaled limit order); IL / exit-cost path. Gate: unit-tested against worked examples.
- [ ] **1.4** Estimators (pure, data-as-input): realized vol, trendiness/path-efficiency, implied vol (`2·fee·√(vol/TVL)·√365`), band survival, fee share. Gate: unit-tested.
- [ ] **1.5** `RangeVerdictCalculator` (→ `Open`/`DoNotOpen` + input snapshot) and `ChannelSimulator` (full series incl. breakouts). Gate: unit-tested; Domain still zero-package, purity tests green. Tag `phase-1-done`.

## Phase 2 — Data adapters & Collector  · precondition `phase-1-done` · tag `phase-2-done`
Read: `ARCHITECTURE.md` §6-7, `DATA-MODEL.md`, `TECH-STACK.md` §2, `NFR.md` §3-4.
- [ ] **2.1** EF Core + SQLite stores realizing `DATA-MODEL.md`; migrations; append-only repositories (insert+query only) for facts/decision/intent. Gate: append-only enforced and tested; idempotent insert-if-absent on natural keys.
- [ ] **2.2** The Graph gateway adapter (`IPoolDataSource`): pool state, day volume, tick liquidity distribution; Uni v3 on Arbitrum + Base descriptors. Gate: integration test vs recorded fixtures; subgraph IDs recorded in PR.
- [ ] **2.3** Nethereum event reader (`IChainEventReader`): mint/burn/collect + gas for configured wallets. Gate: integration test vs recorded fixtures.
- [ ] **2.4** Collector host (Worker + Cronos): scheduled pool/tick snapshots, price refresh, wallet sync; `/health` freshness. Gate: jobs idempotent (test); runs locally with env vars; no secret in repo.
- [ ] **2.5** Deployment artifact (Dockerfile/compose or systemd) + `docs/DEPLOYMENT.md` runbook. Gate (agent): runbook complete. Tag `phase-2-done`.
- [ ] **2.6** *(principal, human)* Deploy to VPS via runbook; confirm snapshots accumulating on both chains. Tick when confirmed.

## Phase 3 — Module 0: LP-Audit  · precondition `phase-2-done` · tag `phase-3-done`
Read: `FSD` UC-01, `DATA-MODEL.md` (Position/PositionEvent/IntentRecord/AuditReport), `LP-KNOWLEDGE.md` §3.
- [ ] **3.1** `AuditWalletPositions` use case + CLI: reconstruct positions from events, fees reconciled with `COLLECT`, IL/gas, vs HODL/50-50/intent benchmark (both benchmarks if reclassified). Deterministic JSON+markdown report. Gate: runs on `config/wallets.json` real wallet; byte-identical on re-run; reconciliation diffs flagged; refuses on insufficient data. Tag `phase-3-done`.

## Phase 4 — Analytical core (API/CLI, NO frontend)  · precondition `phase-3-done` · tag `phase-4-done`
Read: `API-CONTRACT.md`, `FSD` UC-02/03/04/09, `TECH-STACK.md` §1, `NFR.md` §1-2.
- [ ] **4.1** API host skeleton + `X-Api-Key` auth + OpenAPI + RFC7807 errors (incl. `422` insufficient-data). Gate: health endpoint + auth tested.
- [ ] **4.2** `EstimateRangeApr` (`/ranges/estimate-apr`): self-dilution, while-in-range vs time-adjusted, 7d/30d sensitivity. Gate: use-case tested; cross-check vs Metrix documented.
- [ ] **4.3** `ClassifyVolRegime` (`/regime`, `/assets/{id}/regime`): RANGE/TRENDING/TRANSITION + evidence. Gate: use-case tested; never emits direction (RN-07).
- [ ] **4.4** `EvaluateRange` (`/ranges/evaluate`) → verdict + inputs; appends immutable `DecisionLogEntry` (hash) every call; `/decisions` read. Gate: log immutable+retrievable (tested); `422` on thin data.
- [ ] **4.5** Replay UC-09 cat. A (`/ranges/backtest`): band survival, IV-vs-RV, fee-APR reconciliation. Gate: deterministic; **no optimizer/threshold-tuning** (code review + test, RN-14); refuses on thin data.
- [ ] **4.6** `SimulateChannel` (`/channels/simulate`): full series incl. breakouts; rejects incomplete breakout protocol (RN-04). Gate: tested. 
- [ ] **4.7** OpenAPI committed and matching `API-CONTRACT.md`. Gate: spec diff reviewed. Tag `phase-4-done`. **No frontend exists yet.**

## Phase 5 — VALUE VALIDATION GATE (GO/NO-GO)  · precondition `phase-4-done`
Read: `IMPLEMENTATION-PLAN.md` Phase 5, `LP-KNOWLEDGE.md` §6.
- [ ] **5.1** *(agent)* Assemble the **Edge Evidence Dossier** on real data: audit retrospective, mechanism validation (band survival & IV-vs-RV discrimination), estimate fidelity vs realized & Metrix, verdict sanity (descriptive, not edge proof). Stop. Gate: dossier complete and honest (no in-sample edge claims).
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
- [ ] Web screen for UC-09 replay (CLI-first in v1).
