# FollowAlpha.LP — Data Model

Authored 2026-06-14. Binding contract for the persistence layer (EF Core code-first, SQLite → Postgres seam). Same change rules as `ARCHITECTURE.md`. The EF migrations are the executable truth; this document is the agreed model the migrations must realize. Where this conflicts with `ARCHITECTURE.md` §6, that wins.

## 1. Principles (recap, enforced here)

- Every persisted aggregate carries `TenantId` (constant `default` today; SaaS seam). All natural keys below are implicitly scoped by `TenantId`.
- Three data natures, three behaviors:
  - **Facts** (on-chain/market observations): append-only, idempotent re-ingestion via natural keys. Never updated.
  - **Decision records** (decision log, annotations, intents): append-only. Reclassification/annotation = new row, original preserved.
  - **Working state** (watchlist, wallets, alert rules, settings): normal CRUD.
- All timestamps `DateTimeOffset`, UTC. Money/human-scale values `decimal`; raw on-chain integers (`liquidity`, `sqrtPriceX96`) stored as `string`/`BigInteger`-safe text to avoid precision loss.
- Domain vocabulary in English (intents `ACCUMULATE/DISTRIBUTE/HARVEST`, verdict `OPEN/DONT_OPEN`, regimes `RANGE/TRENDING/TRANSITION`).

## 2. Entities

### Working state (CRUD)

**Chain** — `Id` (pk, e.g. `arbitrum`, `base`), `Name`, `RpcEnvVarName`, `Enabled`.

**DexProtocol** — `Id` (pk, e.g. `uniswap-v3`), `ChainId` (fk), `SubgraphId`, `PositionManagerAddress`, `FeeTiers` (json), `Enabled`. (Adding a DEX/chain = new row, per `IDexProtocolRegistry`.)

**Asset** — `Id` (pk, e.g. `ETH`), `Symbol`, `Decimals`, `ChainlinkFeedAddress` (nullable), `InWatchlist` (bool). The watchlist is `Asset` rows flagged `InWatchlist`.

**Pool** — `Id` (pk = chain + pool address), `ChainId` (fk), `DexProtocolId` (fk), `Token0AssetId` (fk), `Token1AssetId` (fk), `FeeTier`, `TickSpacing`, `Address`, `InWatchlist` (bool).

**Wallet** — `Id` (pk), `Address`, `Label`, `Chains` (json list). Audit targets (see `config/wallets.json`).

**AlertRule** (UC-07) — `Id` (pk), `Type` (`PRICE_NEAR_EDGE` | `REGIME_CHANGE` | `POOL_IV_THRESHOLD`), `TargetRef` (position/asset/pool id), `Params` (json), `Enabled`, `NotificationChannelId`.

**AppSetting** — `Key` (pk), `Value`. (Notification channel config, watchlist params, etc.)

### Facts (append-only, idempotent)

**PriceBar** — natural key (`AssetId`, `Resolution`, `OpenTimeUtc`). Fields: `Open/High/Low/Close` (decimal), `Volume`, `Source`. Spot OHLCV for assets.

**PoolSnapshot** — natural key (`PoolId`, `AsOfUtc`). Fields: `CurrentTick`, `SqrtPriceX96` (text), `Liquidity` (text), `Tvl` (decimal), `DayVolumeUsd` (decimal), `Source`. One row per scheduled snapshot.

**TickLiquiditySnapshot** — natural key (`PoolId`, `AsOfUtc`, `Tick`). Fields: `LiquidityNet` (text), `LiquidityGross` (text). The per-tick distribution — the data that cannot be reconstructed retroactively (drives the always-on Collector).

**PositionEvent** — natural key (`ChainId`, `TxHash`, `LogIndex`). Fields: `WalletId` (fk), `PoolId` (fk), `EventType` (`MINT`|`BURN`|`COLLECT`), `TickLower`, `TickUpper`, `LiquidityDelta` (text), `Amount0`, `Amount1`, `FeesCollected0`, `FeesCollected1`, `GasCostUsd`, `BlockTimeUtc`. Source of audit truth (fees reconciled against `COLLECT`).

### Position & intent

**Position** — `Id` (pk, = NFT token id + chain), `WalletId` (fk), `PoolId` (fk), `TickLower`, `TickUpper`, `OpenedAtUtc`, `ClosedAtUtc` (nullable), `Status` (`OPEN`|`CLOSED`). Reconstructed from `PositionEvent`s; treated as a fact-derived projection (rebuildable, not hand-edited).

**IntentRecord** (append-only) — `Id` (pk), `PositionId` (fk), `Intent` (`ACCUMULATE`|`DISTRIBUTE`|`HARVEST`), `DeclaredAtUtc`, `Reason` (nullable), `SupersedesIntentRecordId` (nullable, self-fk). RN-01: reclassification inserts a new row pointing at the prior; the original is never mutated. The "current" intent is the latest record; reports show all benchmarks in the chain.

### Decision records (append-only)

**DecisionLogEntry** — `Id` (pk), `CreatedAtUtc`, `Kind` (`RANGE_VERDICT`|`CHANNEL_SIM`), `PoolId` (fk), `Intent`, `Capital`, `TickLower`, `TickUpper`, `InputsJson` (full input snapshot: IV, forecast RV, fee APR, band survival, IL scenarios, regime), `Verdict` (`OPEN`|`DONT_OPEN`|null for sims), `ExpectancyNet`, `ContentHash` (sha256 of canonical inputs+verdict). Written on every evaluation, even if the user does not open (RN-03). Immutable.

**DecisionAnnotation** (append-only) — `Id` (pk), `DecisionLogEntryId` (fk), `CreatedAtUtc`, `Text`. Dated notes added after the fact (e.g. "não abri porque X"); never alter the entry (RN-03).

### Analysis outputs (persisted for reproducibility)

**BacktestRun** (UC-09) — `Id` (pk), `CreatedAtUtc`, `Type` (`BAND_SURVIVAL`|`IV_VS_RV`|`FEE_APR_RECON`|`CHANNEL`), `ParamsJson`, `ResultJson`, `DataWindowFromUtc`, `DataWindowToUtc`, `InputDataHash`. Deterministic; same params+data → same `ResultJson`. No optimizer fields by design (RN-14).

**AuditReport** (UC-01) — `Id` (pk), `CreatedAtUtc`, `WalletId` (fk), `ResultJson` (per-position + aggregate: fees, IL, gas, vs HODL / 50-50 / intent benchmark), `InputDataHash`. Reproducible (byte-identical for same inputs).

## 3. Relationships (text ERD)

```
Chain 1───* DexProtocol 1───* Pool *───1 Asset (token0/token1)
Chain 1───* Asset
Wallet 1───* Position *───1 Pool
Position 1───* IntentRecord (append-only chain via SupersedesIntentRecordId)
Position 1───* PositionEvent
Pool 1───* PoolSnapshot ; Pool 1───* TickLiquiditySnapshot
Asset 1───* PriceBar
Pool 1───* DecisionLogEntry 1───* DecisionAnnotation
Wallet 1───* AuditReport
AlertRule *───1 (Asset|Pool|Position) [polymorphic via Type+TargetRef]
```

## 4. Append-only enforcement

Repository implementations for `PriceBar`, `PoolSnapshot`, `TickLiquiditySnapshot`, `PositionEvent`, `DecisionLogEntry`, `DecisionAnnotation`, `IntentRecord`, `BacktestRun`, `AuditReport` expose **insert + query only** — no update/delete on the repository interface. Idempotent ingestion uses the natural keys above (insert-if-absent). A unit/integration test asserts the absence of update/delete paths on these aggregates.

## 5. Retention & growth (see NFR.md §4)

`TickLiquiditySnapshot` is the dominant growth driver (per-pool, per-snapshot, per-tick). Snapshot cadence and watchlist size bound it; retention/rollup policy is defined in `NFR.md` and revisited when the Collector's real volume is known. Facts are never deleted to "clean up"; rollup (if needed) writes a derived aggregate and keeps raw rows or archives them — decided with data in hand, not pre-emptively.
