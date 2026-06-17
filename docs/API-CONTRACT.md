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

**Phase 3.2 DTOs are frozen below (decided 2026-06-17).** Money/ratios are decimal strings; raw on-chain
integers (liquidity, sqrtPrice) are strings; timestamps ISO-8601 UTC. Derived metrics that depend on a
snapshot are **never presented from stale data** (see *Staleness* below): a single-resource endpoint returns
`422`, a list row sets `dataStatus` and nulls the derived metrics. No field is "cheap/expensive" — 3.2
reports numbers with their basis, not conclusions.

`GET /v1/assets` → watchlist rows:
```
[{ id, symbol, chain, regime, rvSummary:{ d7, d30, d90 }, asOfUtc, dataStatus }]
```
`regime` is null and `rvSummary` windows are null when history is insufficient (`dataStatus:"INSUFFICIENT"`); `dataStatus ∈ { OK, INSUFFICIENT }`.

`GET /v1/assets/{id}/chart` → Asset View payload (one call, FSD Tela 2). All series server-computed (TECH-STACK Principle 1). **Staged delivery:**
- **Phase 3.2 (headless, now):**
```
{
  candles: [{ openTimeUtc, open, high, low, close, volumeUsd }],   // resolution "1d", ascending
  regimeTimeline: [{ asOfUtc, regime }],                           // regime on the window ending at asOfUtc; only sufficient-history points emitted
  rvVsPoolIv: { realizedVol:{ d7, d30, d90 }, poolIvAverage, ivBasis:"pool_tvl_total", asOfUtc, note }
}
```
  `422` if there are no candles at all. `poolIvAverage` is null when the asset has no pool with a fresh snapshot; `note` is descriptive, never a cheap/expensive verdict.
- **Phase 6 (only after the Phase-5 value gate = GO):** the decorative Asset-View overlays `emaOverlays[]`, `structuralLevels[]`, `empiricalRangeBands[]`, `contextIndicators[]`. These serve the chart UI, which does not exist before Phase 6 and may never exist (NO-GO); building them earlier is gold-plating against a screen that has not earned its place. The generated OpenAPI is the truth for what is implemented at each phase.

`GET /v1/assets/{id}/regime` (RN-07: volatility only, **never direction**) →
```
{
  regime,                              // RANGE | TRENDING | TRANSITION
  evidence: {
    rvPercentile, trendiness,          // current RV percentile within the lookback; path-efficiency [0,1]
    rvWindow, percentileLookback, trendinessWindow,   // bar counts the policy used
    minBars, sampleCount,              // required vs available bars
    asOfUtc, classificationReason      // latest bar time; short text naming the rule that fired
  }
}
```
`422` (insufficient data) when `sampleCount < minBars` or the latest bar is stale — never a guessed regime.

### Pools (UC-02 step 4)

`GET /v1/assets/{id}/pools` → comparison table:
```
[{
  poolId, pair, chain, feeTier, asOfUtc, dataStatus,            // dataStatus ∈ { OK, STALE, NO_SNAPSHOT }
  volumeUsd, tvlUsd, volTvlRatio,
  poolIv: { annualized, basis:"pool_tvl_total", volumeUsd, tvlUsd, asOfUtc },   // fields null unless dataStatus=OK
  competingLiquidity: {
    activeLiquidityAtCurrentTickRaw,                            // = PoolSnapshot.Liquidity (raw L, NOT USD)
    liquidityDensityAroundPriceRaw,                             // summed gross liquidity (raw) over ticks in the band
    bandPct, tickLower, tickUpper, asOfUtc                      // band = ±bandPct of current price; bounds are the band's initialized ticks
  }
}]
```
When `dataStatus ≠ OK` the derived metrics (`volTvlRatio`, `poolIv.value`, `competingLiquidity.*`) are null — a stale snapshot is never shown as a current signal. The list itself does not `422` (the pools exist).

`GET /v1/pools/{poolId}` → pool detail:
```
{
  poolId, pair, chain, feeTier, tickSpacing,
  latestSnapshot: { asOfUtc, currentTick, sqrtPriceX96, liquidity, tvlUsd, dayVolumeUsd, source },
  volTvlRatio,
  poolIv: { annualized, basis:"pool_tvl_total", volumeUsd, tvlUsd, asOfUtc },
  competingLiquidity: { activeLiquidityAtCurrentTickRaw, liquidityDensityAroundPriceRaw, bandPct, tickLower, tickUpper, asOfUtc },
  tickLiquidity: [{ tick, liquidityNet, liquidityGross }]       // distribution at the latest snapshot
}
```
`422` when the pool has no snapshot or the latest snapshot is stale.

**Pool IV basis (honest framing).** `poolIv.annualized` is the Domain formula `2·fee·√(dayVolume/TVL)·√365`
with `basis = "pool_tvl_total"` — the denominator is the pool's **total** TVL, not in-range/active TVL, so it
is an approximation that tends to understate IV for concentrated pools. The inputs (`volumeUsd`, `tvlUsd`)
are echoed so the figure is reproducible. 3.2 returns the number with its basis and draws no cheap/expensive
conclusion.

**Staleness.** A snapshot/tick distribution older than the freshness policy (default `2 ×` the collector's
pool-snapshot cadence) is treated as **not a current signal**: single-resource endpoints (`/regime`,
`/pools/{poolId}`) return `422` with a detail naming the age; list endpoints (`/assets`,
`/assets/{id}/pools`) keep the row but set `dataStatus` (`STALE`/`NO_SNAPSHOT`/`INSUFFICIENT`) and null the
derived metrics.

### Range candidates, APR & verdict (UC-03, Module 2 / Range Advisor)

`POST /v1/ranges/estimate-apr` — req `{ poolId, tickLower, tickUpper, capital }` → `{ feeAprWhileInRange, feeAprTimeAdjusted, volumeSensitivity:{ d7, d30 }, selfDilutionApplied:true }`. Honest APR building block (no IL side here — UI never shows this alone, FSD).

`POST /v1/ranges/candidates` — req `{ poolId, intent, capital?, candidateGrid?:{ widthsPct[], placementMode }, window }` → `{ candidates:[{ tickLower, tickUpper, widthPct, placement, evidence:{ poolIv, forecastRv, feeApr, bandSurvival:{ medianDays, q25, q75 }, ilByExit:{ up, down }, competingLiquidity }, rationale[], dataSufficiency }] }`. Candidate grid is deterministic and predeclared; no optimizer, no threshold tuning, no claim that a historical rule "won".

`POST /v1/ranges/evaluate` — req `{ poolId, tickLower, tickUpper, capital, intent }` → `{ verdict, expectancyNet, inputs:{ poolIv, forecastRv, feeApr, bandSurvival:{ medianDays, q25, q75 }, ilByExit:{ up, down }, regime }, decisionLogId, contentHash }`. Persists a `DecisionLogEntry` on every call (RN-03), even when the caller does not open. 422 if data insufficient (RN-02).

`POST /v1/ranges/backtest` (UC-09, category A only) — req `{ poolId, bandWidthPct, intent?, window }` → `{ bandSurvival{}, ivVsRvOutcome{}, feeAprReconciliation{}, dataSufficiency }`. Descriptive calibration; **no optimizer, no threshold tuning** (RN-14). Refuses (422) rather than infer on thin data.

### Channel (UC-04, Module 3)

`POST /v1/channels/simulate` — req `{ poolId, lower, upper, capital, intent, breakoutProtocol:{ maxReopens, noReopenFloor, capitalCap } }` → `{ events[], pnlSeries[], includesBreakouts:true }`. 400 if `breakoutProtocol` incomplete (RN-04). Result always the full series including breakouts.

### Decision log (UC-05)

`GET /v1/decisions` → filterable list (by pool/intent/verdict/date).
`GET /v1/decisions/{id}` → full entry (inputs + hash + annotations).
`POST /v1/decisions/{id}/annotations` — req `{ text }` → appends a dated annotation (RN-03). The entry itself is immutable (409 on any edit attempt).

### Audit (UC-01, Module 0 / calibration)

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
