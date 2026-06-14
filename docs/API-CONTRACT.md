# FollowAlpha.LP — API Contract

Authored 2026-06-14. The agreed shape of the HTTP API. The **living contract** is the OpenAPI spec generated from the ASP.NET Core code (and the TypeScript client generated from it); this document is the design those must realize. If they diverge, the generated OpenAPI is truth and this doc is updated to match.

## 1. Conventions

- Base path `/v1`. JSON only (`application/json`). UTF-8.
- Auth: `X-Api-Key` header (single key today, `LP_API_KEY`; identity seam for SaaS later).
- All timestamps ISO-8601 UTC. Money as decimal strings to preserve precision; raw on-chain integers as strings.
- Enums on the wire use the constant form: verdict `OPEN`/`DONT_OPEN`, intents `ACCUMULATE`/`DISTRIBUTE`/`HARVEST`, regimes `RANGE`/`TRENDING`/`TRANSITION`.
- The API is **read-mostly**. Writes are limited to: decision log (append), annotations (append), intents (append/reclassify), watchlist, alert rules. No endpoint signs or broadcasts a transaction — ever.
- Errors: RFC 7807 `application/problem+json` with `type`, `title`, `status`, `detail`. Standard codes below.

## 2. Standard error semantics

| Status | When |
|---|---|
| 400 | malformed request (e.g. range incompatible with intent — UC-03 alt flow) |
| 401 | missing/invalid API key |
| 404 | unknown pool/asset/position/decision id |
| 409 | append-only violation attempt (e.g. editing a logged verdict) |
| **422** | **insufficient data to produce a verdict/estimate** — body explains what is missing (RN-02). NOT a guess. |
| 503 | a required data source is unavailable and no cross-check covers it |

## 3. Endpoints

### Assets & regime (UC-02, Module 1)

`GET /v1/assets` → watchlist: `[{ id, symbol, regime, rvSummary }]`.

`GET /v1/assets/{id}/chart` → Asset View payload (one call, FSD Tela 2): `{ candles[], emaOverlays[], structuralLevels[], regimeTimeline[], rvVsPoolIv{}, empiricalRangeBands[], contextIndicators[] }`. All series server-computed (TECH-STACK Principle 1).

`GET /v1/assets/{id}/regime` → `{ regime, evidence:{ rvPercentile, trendiness, windows } }` (RN-07: volatility only, never direction).

### Pools (UC-02 step 4)

`GET /v1/assets/{id}/pools` → comparison table: `[{ poolId, pair, chain, feeTier, volumeUsd, tvlUsd, volTvlRatio, poolIv, competingLiquidity }]`.

`GET /v1/pools/{poolId}` → pool detail incl. latest snapshot + tick liquidity distribution.

### Range APR & verdict (UC-03, Module 2)

`POST /v1/ranges/estimate-apr` — req `{ poolId, tickLower, tickUpper, capital }` → `{ feeAprWhileInRange, feeAprTimeAdjusted, volumeSensitivity:{ d7, d30 }, selfDilutionApplied:true }`. Honest APR building block (no IL side here — UI never shows this alone, FSD).

`POST /v1/ranges/evaluate` — req `{ poolId, tickLower, tickUpper, capital, intent }` → `{ verdict, expectancyNet, inputs:{ poolIv, forecastRv, feeApr, bandSurvival:{ medianDays, q25, q75 }, ilByExit:{ up, down }, regime }, decisionLogId, contentHash }`. Persists a `DecisionLogEntry` on every call (RN-03), even when the caller does not open. 422 if data insufficient (RN-02).

`POST /v1/ranges/backtest` (UC-09, category A only) — req `{ poolId, bandWidthPct, intent?, window }` → `{ bandSurvival{}, ivVsRvOutcome{}, feeAprReconciliation{}, dataSufficiency }`. Descriptive calibration; **no optimizer, no threshold tuning** (RN-14). Refuses (422) rather than infer on thin data.

### Channel (UC-04, Module 3)

`POST /v1/channels/simulate` — req `{ poolId, lower, upper, capital, intent, breakoutProtocol:{ maxReopens, noReopenFloor, capitalCap } }` → `{ events[], pnlSeries[], includesBreakouts:true }`. 400 if `breakoutProtocol` incomplete (RN-04). Result always the full series including breakouts.

### Decision log (UC-05)

`GET /v1/decisions` → filterable list (by pool/intent/verdict/date).
`GET /v1/decisions/{id}` → full entry (inputs + hash + annotations).
`POST /v1/decisions/{id}/annotations` — req `{ text }` → appends a dated annotation (RN-03). The entry itself is immutable (409 on any edit attempt).

### Audit (UC-01, Module 0)

`POST /v1/audit/run` — req `{ walletId }` → `{ auditReportId }` (async if long; status via GET).
`GET /v1/audit/{reportId}` → per-position + aggregate (fees, IL, gas, vs HODL/50-50/intent benchmark; reclassified positions show both benchmarks — RN-01).

### Positions, intents, monitor (UC-08)

`GET /v1/positions` → open positions with monitor data: `{ feesAccrued, ilAccrued, distanceToEdges, verdictPremiseStatus:{ changed, what } }` (UC-08; informational only).
`POST /v1/positions/{id}/intent` — req `{ intent, reason }` → appends an `IntentRecord` (initial or reclassification with trail, RN-01).

### Alerts (UC-07) & config

`GET/POST/DELETE /v1/alert-rules` → CRUD of alert rules.
`GET /v1/health` → collector freshness per pool, last sync, gaps, data-source status (also used by ops).

## 4. Out of scope (explicitly no endpoint)

No transaction build/sign/broadcast; no direction/price prediction; no "buy/sell signal"; no endpoint that mutates a fact, a logged verdict, or an intent record (only appends).
