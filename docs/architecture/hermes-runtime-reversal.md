# Hermes runtime reversal

JoyZoning is the **habitat** — it observes, supervises, reviews, and accept-merges.
Hermes is the **runtime** — it owns execution authority, the operational journal, and convergence gates.

This document describes the corrected integration model after reversing the earlier mistake of embedding Hermes inside JoyZoning.

## Layer boundaries

```txt
JoyZoning   = human-facing convergence habitat (observe / review / merge)
Hermes      = execution authority + journal + gates
JSDP        = bounded mutation handoff
broccolidb  = forensic memory / policy graph
Git/tests   = reality substrate
```

### Authority routing (non-negotiable)

| Rule | Owner |
|------|--------|
| Habitat must not execute tools | JoyZoning |
| Runtime must not self-merge | Hermes |
| Mutation plugin must not become runtime | JSDP plugin (Hermes) |
| Control plane must remain observe-only for runtime events | JoyZoning CP |

**Hermes owns operational state; JoyZoning remains the habitat that observes.**

## Old incorrect model

```txt
JoyZoning
 └── apps/agent-runtime/   ← vendored Hermes on :9090
      └── "JoyZoning executes agent work"
```

Problems:

- Runtime, habitat, mutation, visualization, and convergence collapsed into one layer
- Watch UI implied JoyZoning dispatched workers and owned execution
- Duplicate state: JoyZoning and Hermes both looked authoritative

`apps/agent-runtime/` is **`LegacyRuntimeShim` tombstone** only (HTTP 410 on `:9090`). The vendored Hermes tree was **removed** (pass 8).

## New correct model

```txt
Hermes (external install, e.g. diet-hermes)
 ├── agent/joyzoning/          journal, convergence, habitat_events
 ├── plugins/joyzoning_runtime/ gates + journal hooks
 └── tools/convergence_tools   runtime-owned convergence API

JoyZoning (:9470 control plane)
 ├── POST /api/internal/hermes-observation   ingest (non-authoritative)
 ├── GET  /api/hermes/convergence/{scopeId}  display mirror
 ├── POST /api/tasks/{id}/dispatch           request managed Hermes run (not local execute)
 └── POST /api/tasks/{id}/lease/merge        operator accept-merge (habitat-owned)
```

JoyZoning **concepts** (convergence, JSDP handoff, habitat events) are infused into Hermes.
JoyZoning does **not** embed the runtime.

## Observation flow

1. Hermes appends to `~/.hermes/joyzoning/journal.db` (canonical).
2. Hermes mirrors events to JoyZoning when `joyzoning.control_plane.url` is set and `observe_only: true`.
3. JoyZoning ingests via `POST /api/internal/hermes-observation` with `authoritative: false`.
4. Watch UI reads `GET /api/hermes/convergence/{scopeId}` — **display only**, never for authorization.

Requests with `authoritative: true` are rejected (`403`).

## What JoyZoning still owns

- **ExecutionLease** — lease grant/revoke/heartbeat
- **Accept-merge** — `POST /api/tasks/{id}/lease/merge` (git convergence into main workspace)
- **Operator review** — verification reports, merge queue, decision preflight
- **JSDP chain UI** — role sequencing; handoff validation stays on Hermes (`jsdp_validate_handoff`)

## What JoyZoning must not do

- Dispatch tools directly
- Mutate workspace files except through explicit merge/lease mechanics
- Treat ingested observations as authoritative for convergence authorization
- Self-authorize `convergence.converged` or `ready_for_review` from habitat events

## Configuration

Point JoyZoning at the external Hermes install (not `apps/agent-runtime`):

```yaml
Hermes:
  InstallRoot: /path/to/diet-hermes-main-master
  ApiBaseUrl: http://127.0.0.1:8787   # gateway / API
```

On Hermes:

```yaml
joyzoning:
  enabled: true
  emit_habitat_events: true
  control_plane:
    url: http://127.0.0.1:9470
    observe_only: true
```

## UX language

| Old (misleading) | New (accurate) |
|------------------|----------------|
| Worker dispatched | Hermes run started (supervised) |
| JSDP worker orchestration | Runtime observers (JSDP chain) |
| Start local agent runtime | Connect to Hermes runtime |
| JoyZoning executes | JoyZoning supervises |

## Production hardening (pass 2)

### Hermes-side

| Control | Location |
|---------|----------|
| `pre_tool_call` returns `{"action":"block","message":...}` | `plugins/joyzoning_runtime` + `agent/joyzoning/convergence_gate.py` |
| `kanban_db.complete_task` enforces same gate | CLI/dashboard cannot bypass agent hook |
| `observe_only: false` rejected when CP URL set | `agent/joyzoning/config.py` |
| CP URL SSRF allowlist (localhost only) | `agent/joyzoning/control_plane_client.py` |
| Ingest token header | `JOYZONING_INGEST_TOKEN` → `X-JoyZoning-Internal-Token` |

### JoyZoning-side

| Control | Location |
|---------|----------|
| `authoritative: true` → 403 | `HermesObservationIngestService` |
| Non-`hermes-runtime` source → 403 | same |
| Internal token on ingest/verification/agent-status | `HabitatInternalAuth` |
| Autopilot default **off** | `AuthorityOptions.AutopilotEnabled = false` |
| LegacyRuntimeShim never canonical InstallRoot | `HermesInstallPaths` |
| Desktop/Watch copy: “Request Hermes run” | Kanban + operator-labels |

### Remaining intentional habitat authority

- `POST /api/tasks/{id}/lease/merge` — operator accept-merge (git)
- Lease grant/revoke/review UI
- External task complete with `operatorApproved`

### Production hardening (pass 6)

| Control | Location |
|---------|----------|
| Desktop + CLI “Request Hermes run” copy | `KanbanView.axaml`, `KanbanViewModel`, `ManagerChatViewModel`, `CliOutput.cs` |
| Technical glossary aligned | `docs/glossary.md` |
| BroccoliQ uses `_read_scope_env` + scope alias on worker start | `kanban_broccolidb_bridge.py`, `plugins/kanban_broccolidb` |
| API route `/dispatch` unchanged (compat); semantics = habitat requests run | `ApiEndpoints.cs` `runtimeOwner` / `habitatRole` |

### Production hardening (pass 5)

| Control | Location |
|---------|----------|
| JoyZoning scope via `contextvars` (not `os.environ`) on API runs | `gateway/session_context.py` + `api_server.py` |
| `resolve_scope_id` reads gateway context first | `agent/joyzoning/config.py` |
| BroccoliQ hive rows include `convergence_state` + habitat scope | `tools/kanban_broccolidb_bridge.py` |
| Deterministic git-failure test double | `FailingWorkspaceGitMerger` |
| Onboarding/docs: “Request Hermes run” not “Dispatch executes” | `docs/onboarding/*`, `concepts.md` |

### Production hardening (pass 4)

| Control | Location |
|---------|----------|
| Scope cluster (habitat GUID + kanban `t_…` + session) | `agent/joyzoning/scope_registry.py` |
| Bridge marks all linked scopes `CONVERGED` | `habitat_bridge.py` + `HermesHabitatBridgeService` |
| Dispatch pins `JOYZONING_HABITAT_TASK` / `JOYZONING_SCOPE_ID` | `OrchestrationService` → Hermes `metadata` |
| API runs apply metadata env (thread-local restore) | `gateway/platforms/api_server.py` |
| BroccoliQ hive payload carries habitat scope | `tools/kanban_broccolidb_bridge.py` |

### Production hardening (pass 3)

| Control | Location |
|---------|----------|
| Habitat accept-merge → Hermes `CONVERGED` | `HermesHabitatBridgeService` + `scripts/joyzoning_habitat_ack.py` |
| Bridge token | `JOYZONING_HABITAT_BRIDGE_TOKEN` (falls back to `ControlPlane:InternalToken`) |
| `mutation_verify` before `convergence_request_review` | `agent/joyzoning/mutation_lifecycle.py` |
| Journal WAL + integrity check | `agent/joyzoning/journal.py` |
| Ingest dedupe (10m TTL) + rate limit (120/min) | `HermesObservationDedupe`, `HermesObservationRateLimiter` |
| Non-dev requires `InternalToken` on agent routes | `HabitatInternalAuth` |

## Authority checklist

On startup, Watch and the control plane expose `GET /api/habitat/authority-checklist`:

| Check | Meaning |
|-------|---------|
| Hermes runtime owner detected | External Hermes API reachable |
| JoyZoning habitat role: observe-only | Habitat never executes tools |
| Control plane URL configured | `ControlPlane:ListenUrl` set |
| External Hermes InstallRoot | Not `apps/agent-runtime` |
| LegacyRuntimeShim inactive | Embedded shim is not canonical runtime |
| Hermes observation mirror | `joyzoning.control_plane.url` in Hermes config |

## Tests

See `tests/JoyZoning.Tests/HabitatLayerBoundaryTests.cs` and
`apps/joyzoning/src/test/hermes-observation-e2e.test.tsx` for boundary enforcement.

## Pass 8 — orphan removal

| Removed / tombstoned | Replacement |
|----------------------|-------------|
| Vendored Hermes under `apps/agent-runtime/` (~600MB) | External `Hermes:InstallRoot` |
| `packages/agent-bridge` HTTP client | Control plane `/api/hermes/*` |
| `setup.ts` Python venv in `apps/agent-runtime/` | `hermes setup` in external checkout |
| `fusion-architecture.md` old “manager/worker” model | Superseded banner + link here |

`pnpm --filter @joyzoning/agent-runtime dev` only serves **410** JSON; `dev-all.ts` does not start it.

## Pass 9 — integration hardening

| Fix | Location |
|-----|----------|
| `session.end` hook missing `_read_scope_env` import | `plugins/joyzoning_runtime/__init__.py` |
| Habitat journal uses gateway contextvars for session/run ids | `agent/joyzoning/habitat_events.py` |
| Kanban hive sync uses `_read_scope_env` (not raw `os.environ`) | `tools/kanban_broccolidb_bridge.py` |
| Async hive executor shuts down on process exit | `atexit` in `kanban_broccolidb_bridge.py` |
| `workspace-core` / `shared-contracts` / `agent-bridge` marked legacy | `packages/*/REMOVED.md` |
| Setup builds only agent-runtime tombstone | `scripts/setup.ts` |
