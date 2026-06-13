# FollowAlpha.LP — Tech Stack Specification (analysis layer)

Authored 2026-06-12. Binding for implementation, same change rules as `ARCHITECTURE.md`. Constraint set by the principal: **best-of-market open-source or free tier only.** Every external dependency below is pinned with license/limits; free-tier limits are approximate and must be re-verified at implementation time (they change), recorded in the PR that wires each one.

## 0. Principles

1. **Indicators are computed server-side.** The frontend renders series delivered by the API; it never computes an indicator. One source of truth: the number on the chart is the number that fed the verdict.
2. **Verdict-feeding math is ours; context math can be a library.** Anything that enters `RangeVerdictCalculator` (realized vol, trendiness, survival, fee share, IV) is hand-implemented in `Domain` (pure, golden/unit-tested). Context indicators on the Asset View (EMA/SMA/ATR/ADX/RSI/Bollinger, RN-13-gated) use a battle-tested library in `Application`.
3. **Every market data point has a primary source and a cross-check.** Single-sourced numbers are flagged in the UI.
4. **No paid dependencies.** If a free tier dies, the adapter swaps behind the port — that's what the ports are for.

## 1. Backend stack (.NET 10)

| Concern | Choice | License / tier | Notes |
|---|---|---|---|
| API host | ASP.NET Core minimal APIs | MIT | OpenAPI via built-in + Swashbuckle |
| ORM / persistence | EF Core + SQLite | MIT / public domain | migrations in repo; Postgres seam per ARCHITECTURE |
| EVM client | Nethereum | MIT | event logs (mint/burn/collect), Chainlink feed reads |
| Context indicators | **Skender.Stock.Indicators** | Apache-2.0 | the reference .NET TA library (EMA, SMA, ATR, ADX, RSI, BB, +100); lives in `Application`, never in `Domain` |
| Scheduling | .NET `BackgroundService` + **Cronos** (cron parsing) | MIT | no Quartz; keep the Collector boring |
| HTTP resilience | Polly (via `Microsoft.Extensions.Http.Resilience`) | BSD-3 | retries/backoff for all data sources |
| GraphQL queries | plain `HttpClient` + raw query strings | — | The Graph queries are static; a GraphQL client lib is dead weight |
| Logging | Serilog | Apache-2.0 | structured, file + console sinks |
| Tests | xUnit + FluentAssertions + NSubstitute + **NetArchTest.Rules** | OSS | architecture tests are part of the suite |

`Domain` remains BCL-only: realized vol (stdev of log returns), trendiness (path-efficiency ratio), IV formula, survival estimator, liquidity math — all hand-written there with tests. Skender is for the Asset View's context layer only.

## 2. Data sources matrix

| Data | Primary | Cross-check / fallback | Cost & limits (verify at impl.) |
|---|---|---|---|
| Pool state, day volume, tick liquidity distribution | **The Graph decentralized gateway** (Uniswap v3 subgraphs, Arbitrum + Base) | GeckoTerminal pool endpoints | Graph free plan ~100k queries/mo with API key |
| Pool OHLCV (pool-level price candles) | **GeckoTerminal API** | derived from subgraph swaps if needed | free, no key, ~30 req/min |
| Asset spot candles (ETH, BTC, majors; daily + intraday) | **Coinbase Exchange public candles** (pipeline proven in Program 1: paginated, needs User-Agent) | Binance public klines | free, generous public limits |
| Spot price sanity check | **Chainlink price feeds read via RPC** (free contract reads on Arbitrum/Base) | CoinGecko simple price (free ~30/min) | only RPC cost |
| On-chain position events (mint/burn/collect) | **Alchemy free tier RPC** (best `eth_getLogs` ergonomics; supports Arbitrum + Base) | public RPCs (`arb1.arbitrum.io/rpc`, `mainnet.base.org`) | Alchemy free ~300M CU/mo — far above our needs |
| Gas costs (audit) | receipts via the same RPC | — | — |
| TVL/macro context (optional, later) | DefiLlama API | — | free, no key |

Note on the word "oracle": in this repo it means two things — (a) Chainlink **price oracles**, used only as read-only sanity checks (we never execute, so we don't depend on oracle freshness); (b) the **golden-fixture oracle** (`tools/oracle/`, the Elsts Python reference) that validates the math kernel. Different things, both read-only.

## 3. Frontend stack (Next.js)

| Concern | Choice | License | Notes |
|---|---|---|---|
| Framework | Next.js + TypeScript | MIT | App Router |
| Financial charts | **TradingView Lightweight Charts** | Apache-2.0 | the market-standard OSS candlestick engine: candles, line/area overlays (EMAs, structural levels), histogram (volume), price lines & shaded areas (range bands, channel) |
| General viz | **Recharts** | MIT | liquidity-depth histograms, fees-vs-IL race bars, audit tables' sparklines |
| API client | **openapi-typescript + openapi-fetch** generated from the .NET OpenAPI spec | MIT | contract-first; no hand-written fetch code |
| Server state | TanStack Query | MIT | caching, polling for monitor/alerts |
| UI kit | Tailwind CSS + shadcn/ui | MIT | boring, fast, standard |

Explicitly rejected: TradingView **Advanced Charts / widget** (free but closed-source, requires approval, pulls external scripts — and its 100+ built-in client-side indicators would violate Principle 1); D3 from scratch (cost without benefit); any paid charting SDK.

### Indicator roster for the Asset View (analyst recommendation — TO CONFIRM against real data)

Two buckets. The **decision math is already fixed** (lives in `Domain`, feeds the verdict); the **context indicators are a starting recommendation**, to be confirmed once the Collector has accumulated real data per the deferral in `STACK-DECISIONS.md §8` and RN-13.

Decision math (FIXED, `Domain`, hand-written, tested — these are the indicators that matter for LP):
- realized volatility cone (7/30/90d, stdev of log returns);
- pool IV vs forecast realized vol (the cheap/expensive-vol signal);
- empirical band survival (time-to-exit distribution by regime);
- trendiness / path-efficiency (RANGE vs TRENDING classification);
- fee share (own L vs competing in-range liquidity).

Context indicators (RECOMMENDED starting set, Skender, labeled "context, not signal", RN-13):
- **core context**: Bollinger Band width (visual proxy for vol compression/expansion — directly LP-relevant), ATR, long structural moving averages (reference "center" for range placement);
- **secondary**: ADX (trend strength, corroborates the regime label);
- **last / optional**: RSI — most directional, least range-relevant; include only if the principal wants it, and only as visual context, never as a buy/sell signal.

Confirmation rule: the final roster is locked after the Collector runs and we see which indicators actually discriminate good LP setups from bad ones — not decided in the abstract (the lesson from the falsified Programs 1-2). Adding/removing an indicator updates this section and `STACK-DECISIONS.md §8`.

### Chart composition for the Asset View (FSD Tela 2)

All series come from one API payload per asset (`/assets/{id}/chart`):

- candles (OHLCV) — Lightweight Charts candlestick series;
- EMA/SMA overlays + structural levels — line series / price lines (computed by Skender server-side);
- regime timeline (RANGE/TRENDING/TRANSITION) — colored background bands (time-range markers);
- empirical range bands ("±10% held median 21d in this regime") — shaded area series around current price;
- RV vs pool-IV panel — secondary pane, two line series;
- RN-13 context indicators (RSI/ADX/BB) — secondary panes, clearly labeled "context, not signal".

## 4. Integration flow

```
The Graph ──┐                                   ┌─► /assets/{id}/chart  ──► Lightweight Charts
GeckoTerminal┤                                  ├─► /ranges/estimate-apr ─► Range evaluator UI
Coinbase ────┼─► Collector ─► SQLite ─► Application ─► /ranges/evaluate ──► verdict + decision log
Chainlink ───┤   (24/7 VPS)   (facts,    (Skender ctx │  /regime, /audit,
Alchemy RPC ─┘                append-only) + Domain    │  /decisions, /channels
                                          verdict math)└─► alerts via INotificationChannel
```

Rules: the Collector is the only writer of market facts; the API is read-mostly (writes: decision log, annotations, intents, watchlist, alert rules); the frontend talks only to the API.

## 5. Env vars added by this spec

Extends `ARCHITECTURE.md` §9: `GECKOTERMINAL_BASE_URL` (default public), `COINBASE_BASE_URL` (default public), `CHAINLINK_FEEDS` (per-asset feed addresses in `appsettings.json`, not secret), `ALCHEMY_API_KEY` (preferred over raw `RPC_URL_*` when present).
