# LegacyRuntimeShim (`@joyzoning/agent-runtime`)

> **Removed.** The vendored Hermes tree (~600MB) that lived here was deleted in the runtime-reversal pass.
> This package is a **tombstone** only: a tiny TypeScript server that returns **HTTP 410** on port **9090**.

## Status

| Aspect | Canonical | This tombstone |
|--------|-----------|----------------|
| Execution authority | External Hermes install | None |
| Operational journal | `~/.hermes/joyzoning/journal.db` | None |
| Habitat observation | JoyZoning CP ingest | None |
| Purpose | Production runtime | Migration guardrail only |

## What to use instead

1. Install/run Hermes from your external checkout (e.g. `diet-hermes-main-master`).
2. Configure JoyZoning `Hermes:InstallRoot` and `Hermes:ApiBaseUrl` in control plane settings.
3. Use JoyZoning control plane `GET /api/hermes/health` — not `:9090/health`.
4. Enable Hermes `joyzoning.control_plane.url` with `observe_only: true` for observation mirror.

Architecture: [`docs/architecture/hermes-runtime-reversal.md`](../../docs/architecture/hermes-runtime-reversal.md).

## If something still starts this package

`pnpm --filter @joyzoning/agent-runtime dev` only serves deprecation JSON. `dev-all.ts` does **not** start it by default.
