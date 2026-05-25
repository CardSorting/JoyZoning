# Fusion Architecture (superseded)

> **This document described the pre-reversal model** where JoyZoning embedded a vendored Hermes under `apps/agent-runtime/` and used `packages/agent-bridge` on port `:9090`.
>
> That model is **retired**. The vendored tree was removed; `agent-bridge` is a tombstone package.

## Canonical architecture

Read **[Hermes runtime reversal](architecture/hermes-runtime-reversal.md)** — the single source of truth:

| Layer | Role |
|-------|------|
| **JoyZoning** | Habitat — observe, supervise, review, accept-merge |
| **Hermes** (external `InstallRoot`) | Runtime — execution, journal, convergence gates, tool dispatch |
| **JSDP** | Bounded mutation handoff (Hermes-mediated) |
| **BroccoliDB / BroccoliQ** | Forensic memory / policy graph (non-authoritative) |
| **Git + tests** | Reality substrate |

**Hard rule:** habitat observation ingest with `authoritative: true` → **403**.

## What was removed (pass 8)

- `apps/agent-runtime/` vendored Hermes (~600MB) — replaced by a 410 tombstone server
- `packages/agent-bridge` HTTP client — constructor throws; use control plane APIs
- `setup.ts` Python venv bootstrap under `apps/agent-runtime/`

## Workspace layout (current)

```
JoyZoning/
  apps/
    joyzoning/          # Watch UI (Next.js)
    agent-runtime/      # LegacyRuntimeShim tombstone only (:9090 → 410)
  packages/
    shared-contracts/   # Event shapes (control plane / Watch)
    workspace-core/     # Path containment helpers
    agent-bridge/       # Deprecated tombstone (do not import)
  src/
    JoyZoning.ControlPlane/
    JoyZoning.App/
  docs/architecture/hermes-runtime-reversal.md
```

## Communication rules (current)

1. **No execution in JoyZoning** — dispatch requests a managed Hermes run; habitat never calls agent tools directly.
2. **No `AgentBridgeClient`** — use control plane `/api/hermes/*` and Hermes gateway/API from `InstallRoot`.
3. **Contracts** — `packages/shared-contracts` remains the typed boundary for UI ↔ control plane events.
