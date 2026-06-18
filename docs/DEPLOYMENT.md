# DEPLOYMENT.md — FollowAlpha.LP DataSync (VPS runbook)

The DataSync is the only deployable in Phase 2 (`IMPLEMENTATION-PLAN.md §2`). It is an always-on host
(`ARCHITECTURE.md §7`) that runs scheduled jobs and exposes a `/health` endpoint. It is **designed
always-on**: the per-tick liquidity distribution it snapshots cannot be reconstructed retroactively. A
missed run is recoverable for events and prices, **never** for tick distributions — so once it is running
24/7 the box should stay up and the database must live on durable storage.

> **Read-only on-chain.** The DataSync only reads The Graph and public JSON-RPC. There is no signing code
> and no private key anywhere in this system. Never add one.

> **Deployment timing (decided 2026-06-17).** `phase-2-done` only requires this to be **deploy-ready**
> (image builds + boots, runbook complete). The **always-on Oracle/VPS deployment is intentionally deferred
> until after Phase 3 full has proven sufficient value/edge** — we do not pay for 24/7 infrastructure before
> the engine earns it. Until then, **running the DataSync locally or intermittently is acceptable** for
> smoke and initial collection. Accept that **tick-liquidity gaps during any downtime are permanent and are
> never synthetically backfilled** (`pool-snapshot`/`wallet-sync`/`price-refresh` close their own gaps on the
> next run via idempotent re-ingestion and the wallet-sync cursor; the tick distribution does not). When the
> always-on deploy is actually performed (`CHECKLIST.md` 2.6), the steps below are the runbook for it. Agents
> never receive VPS credentials.

---

## 1. What gets deployed

| Component | Project | Role |
|---|---|---|
| DataSync host | `src/FollowAlpha.LP.DataSync` | `pool-snapshot` + `wallet-sync` + `price-refresh` cron jobs, seeding, `/health` |

Three scheduled jobs (defaults in `appsettings.json`, overridable via the `DataSync` config section or env):

| Job | Default cron | What it captures |
|---|---|---|
| `pool-snapshot` | `0 * * * *` (hourly) | Pool state, latest day volume, **full per-tick liquidity distribution** (append-only) |
| `wallet-sync` | `*/15 * * * *` (every 15 min) | Audit-wallet mint/burn/collect events, **attributed by owner-at-time**, enriched + append-only |
| `price-refresh` | `5 0 * * *` (daily) | Daily USD OHLCV (`PriceBar`) per watchlist token, from The Graph `tokenDayData` |

All jobs also run once at startup when `DataSync:RunJobsOnStartup=true` (the default), so a fresh deploy
fills data without waiting for the first tick. All are **idempotent** — re-running over an overlapping
window inserts nothing new (pool snapshots key on the run timestamp; position events on `(chain, txHash,
logIndex)`; price bars on `(asset, resolution, openTime)`). A single failing pool/wallet/asset is logged and
never tears down the host.

**Wallet-sync is incremental and owner-aware.** It resumes from a persisted per-(chain, wallet) cursor
(rewound by `WalletSyncReorgBuffer` blocks to absorb shallow reorgs) instead of rescanning from genesis,
and `eth_getLogs` is chunked by `RpcMaxBlockSpan` so a wide first scan does not exceed provider limits.
Events are attributed to a wallet only while it actually held the position NFT (built from `Transfer`
in/out intervals) — a position transferred out is never mis-attributed into the append-only audit truth.

---

## 2. Prerequisites

- A Linux VPS (always-on) with Docker, **or** the .NET 10 runtime if you prefer running the published
  binary directly under systemd.
- Outbound HTTPS to The Graph gateway and the configured RPC endpoints.
- Persistent disk for the SQLite database (this is the irrecoverable tick history — back it up).

---

## 3. Configuration

### 3.1 Environment variables (the contract — `ARCHITECTURE.md §9`)

Values are **never** committed. Copy `.env.example` to `.env` and fill it in.

| Variable | Required | Purpose |
|---|---|---|
| `GRAPH_API_KEY` | yes | The Graph decentralized gateway key (pool snapshots) |
| `RPC_URL_ARBITRUM` | one of these per chain | Arbitrum One JSON-RPC endpoint (wallet sync) |
| `RPC_URL_BASE` | one of these per chain | Base JSON-RPC endpoint (wallet sync) |
| `ALCHEMY_API_KEY` | optional | When set and `RPC_URL_<CHAIN>` is empty, the DataSync builds the Alchemy URL for that chain |
| `LP_DB_PATH` | no (defaults) | SQLite file path. Default `./data/followalpha-lp.db`; set to the volume path in containers |

Either set `RPC_URL_ARBITRUM` / `RPC_URL_BASE` explicitly, or set `ALCHEMY_API_KEY` and let the DataSync
derive `https://arb-mainnet.g.alchemy.com/v2/<key>` and `https://base-mainnet.g.alchemy.com/v2/<key>`. An
explicit `RPC_URL_<CHAIN>` always wins.

### 3.2 Watchlist (`appsettings.json` → `DataSync:Watchlist`)

The pools to snapshot and seed live in the `DataSync:Watchlist` section. Each entry seeds the pool, its
two assets, and is snapshotted every `pool-snapshot` run. Subgraph IDs and the position-manager address come
from the DEX protocol registry (`DefaultDexProtocols`), not from here.

### 3.3 Audit wallets (`config/wallets.json`)

The wallet-sync targets. Format:

```json
{
  "tenantId": "default",
  "wallets": [
    { "label": "main", "address": "0x…", "chains": ["arbitrum", "base"] }
  ]
}
```

`config/wallets.json` is resolved relative to the content root, walking up to the repo root. If the file is
missing, the wallet-sync job logs and idles (it is not an error). The watchlist wallet address is public
audit data, not a secret.

---

## 4. Deploy with Docker (recommended)

Build from the **repo root** so the context has the props files and every referenced project:

```bash
docker build -f src/FollowAlpha.LP.DataSync/Dockerfile -t followalpha-lp-datasync .
```

Run with the database on a named volume and the config mounted read-only:

```bash
docker run -d --name followalpha-datasync \
  --restart unless-stopped \
  --env-file .env \
  -v followalpha-lp-data:/app/data \
  -v "$(pwd)/config:/app/config:ro" \
  -v "$(pwd)/src/FollowAlpha.LP.DataSync/appsettings.json:/app/appsettings.json:ro" \
  -p 8080:8080 \
  followalpha-lp-datasync
```

- `LP_DB_PATH` defaults to `/app/data/followalpha-lp.db` inside the image; the named volume
  `followalpha-lp-data` makes the tick history durable across restarts and image upgrades. **Do not** run
  without this volume — you would lose the irrecoverable data on every redeploy.
- The host listens on `8080` (the aspnet image default); `/health` is the readiness probe.
- `--restart unless-stopped` keeps the always-on guarantee across reboots.

### docker-compose alternative

```yaml
services:
  datasync:
    build:
      context: .
      dockerfile: src/FollowAlpha.LP.DataSync/Dockerfile
    restart: unless-stopped
    env_file: .env
    ports:
      - "8080:8080"
    volumes:
      - followalpha-lp-data:/app/data
      - ./config:/app/config:ro
      - ./src/FollowAlpha.LP.DataSync/appsettings.json:/app/appsettings.json:ro
volumes:
  followalpha-lp-data:
```

---

## 5. Deploy without Docker (systemd alternative)

```bash
dotnet publish src/FollowAlpha.LP.DataSync/FollowAlpha.LP.DataSync.csproj -c Release -o /opt/followalpha-datasync
```

`/etc/systemd/system/followalpha-datasync.service`:

```ini
[Unit]
Description=FollowAlpha.LP DataSync
After=network-online.target
Wants=network-online.target

[Service]
WorkingDirectory=/opt/followalpha-datasync
ExecStart=/usr/bin/dotnet /opt/followalpha-datasync/FollowAlpha.LP.DataSync.dll
EnvironmentFile=/opt/followalpha-datasync/.env
Environment=LP_DB_PATH=/var/lib/followalpha/followalpha-lp.db
Restart=always
RestartSec=10
User=followalpha

[Install]
WantedBy=multi-user.target
```

```bash
install -d -o followalpha /var/lib/followalpha          # durable DB location, owned by the service user
systemctl daemon-reload && systemctl enable --now followalpha-datasync
```

The DB path (`/var/lib/followalpha`) must be on durable, backed-up storage for the same reason as the Docker
volume.

---

## 6. Verify a run (local smoke, or the deferred always-on deploy)

1. **Health** — `curl -s localhost:8080/health | jq`. Expect `status: "Healthy"` once the first snapshot
   lands. A pool with no snapshot, or one older than `2 × PoolSnapshotFreshnessSeconds`, makes the endpoint
   return `503` with `status: "Degraded"` and per-pool `stale: true`. The body also reports the last run
   time of each job.
2. **Logs** — `docker logs -f followalpha-datasync` (or `journalctl -u followalpha-datasync -f`). Expect a
   startup "Seeded reference graph" line, then per-pool snapshot lines (`snapshot inserted, N tick rows`)
   and per-wallet sync lines (`N tokenIds, R events read, I inserted`).
3. **Data accumulating on both chains** — confirm pool snapshots and tick rows are growing for each
   watchlist chain, and wallet events for each configured wallet/chain. This is the evidence to log when the
   always-on deploy (`CHECKLIST.md` 2.6) is eventually performed; for local smoke it simply confirms the
   pipeline works end-to-end with real keys.

Quick DB sanity check:

```bash
sqlite3 /path/to/followalpha-lp.db \
  "SELECT PoolId, COUNT(*), MAX(AsOfUtc) FROM PoolSnapshots GROUP BY PoolId;"
```

---

## 7. Operations

- **Backups.** The SQLite file is the only stateful artifact and holds the irrecoverable tick history. Back
  it up regularly (`sqlite3 db ".backup '/backups/lp-$(date +%F).db'"` for a consistent copy while running).
- **Upgrades.** Rebuild the image / republish, then restart the container/service. Migrations run
  automatically at startup (`db.Database.MigrateAsync()`); the data volume is preserved. Seeding is
  insert-if-absent, so it is safe on every start.
- **Recovery after downtime.** On restart the startup runs re-sync events and prices (idempotent, so gaps
  close themselves). Tick-distribution gaps during downtime are permanent — minimize downtime.
- **Schedules.** Tune `DataSync:PoolSnapshotCron` / `DataSync:WalletSyncCron` (Cronos 5-field cron, UTC)
  via config or env. `WalletSyncFromBlock` sets the lower bound for event scans (0 = from genesis).
- **Secrets.** Never bake `.env` or keys into an image (`.dockerignore` excludes them). Rotate by updating
  `.env` and restarting.

---

## 8. Troubleshooting

| Symptom | Likely cause | Action |
|---|---|---|
| `/health` `503`, pools `stale` | No snapshot yet, or job failing | Check logs for the per-pool error; verify `GRAPH_API_KEY` and connectivity |
| Wallet sync logs "no seeded wallets" | `config/wallets.json` missing or not mounted | Mount `config/` (Docker) or place the file at the content/repo root |
| Wallet sync fails on a chain | Missing/invalid `RPC_URL_<CHAIN>` or `ALCHEMY_API_KEY` | Set the endpoint for that chain; an explicit URL overrides Alchemy |
| Data lost after redeploy | DB not on a persistent volume | Ensure the `/app/data` volume (Docker) or durable `LP_DB_PATH` (systemd) — the tick history cannot be rebuilt |
| Startup fails on migrate | DB path not writable | Ensure the service user owns the DB directory |
