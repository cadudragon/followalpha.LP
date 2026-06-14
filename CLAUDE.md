# CLAUDE.md — FollowAlpha.LP

Decision-support tool for concentrated-liquidity LPing (.NET 10 backend + Next.js frontend). It audits LP positions, classifies volatility regimes, prices ranges (OPEN / DON'T OPEN verdicts) and simulates channel strategies. **It recommends; the human executes. Read-only on-chain — no signing code, ever.**

## Read first

1. `AGENTS.md` — the binding rules for any coding agent in this repo (dependency law, Domain purity, golden tests, append-only stores, secrets). **Everything in it applies to Claude too.**
2. `LP-KNOWLEDGE.md` — domain knowledge: LP economics, intent accounting (the core principle), the 4 modules, research discipline, glossary.
3. `docs/ARCHITECTURE.md` — architecture contract (modular monolith, hexagonal, layer rules, configuration contract).
4. `docs/IMPLEMENTATION-PLAN.md` — phases 0-6 with definition of done; work strictly in order.

## Role division

- **Principal (Carlos)**: product owner; decides capital, scope, and product/research ambiguities. Communicates in Portuguese (pt-BR).
- **Quant analyst / architect (Claude, historically)**: authored the contracts; adjudicates research questions (benchmarks, formulas, intent rules) and architecture changes. Architecture changes require updating `docs/ARCHITECTURE.md` with rationale — never silent drift.
- **Implementer (Codex or any coding agent)**: implements mechanically against the contracts.

If you (Claude) are asked to implement: follow `AGENTS.md` exactly, like any other agent. If you are asked to judge results or change contracts: that is the analyst role — be skeptical, demand evidence, keep the discipline in `LP-KNOWLEDGE.md` §6.

## Project state markers

- Phases complete are tagged `phase-N-done` in git. Check tags before assuming what exists.
- Audit target wallet: `config/wallets.json`. Per-position intents: `config/intents.json` (created in Phase 3).
- Golden fixtures: `tests/FollowAlpha.LP.Domain.Tests/Golden/fixtures.json`, generated only by `tools/oracle/` (Python reference, vendored). Never hand-edit.

## Commands (valid once Phase 0 lands)

```powershell
dotnet build FollowAlpha.LP.slnx
dotnet test FollowAlpha.LP.slnx          # includes architecture tests — must stay green
```

Frontend (`frontend/`, from Phase 5): `npm run dev` / `npm run build` / `npm run lint`.

## Non-negotiables worth repeating

- Domain references nothing and performs no I/O; architecture tests enforce this — fix code, never tests.
- Decision log and on-chain fact tables are append-only.
- No secrets in the repo; env var names are in `docs/ARCHITECTURE.md` §9.
- `TenantId` on every persisted aggregate (single `default` tenant today; SaaS-shaped tomorrow).
- The vault portfolio and the research history live in `FollowAlpha.Lean` (separate repo, maintenance mode) — this repo never touches them.
