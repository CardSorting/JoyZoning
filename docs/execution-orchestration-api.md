# Execution orchestration API

HTTP contract for Phase 21–24 **managed** kanban execution leases on the JoyZoning control plane (`http://127.0.0.1:9470`).

> **External-agent JSDP (Cursor, manual):** no lease row — use [control-plane-api.md — External execution](control-plane-api.md#external-execution-no-lease) and [external-agent-jsdp.md](external-agent-jsdp.md). `GET /api/tasks/{id}/lease` returning **404** is expected for external tasks.

Errors are always JSON (never stack traces):

```json
{ "error": "lease_conflict", "message": "Human-readable detail." }
```

| HTTP | `error` | When |
|------|---------|------|
| 400 | `lease_invalid_request` | Missing body fields, invalid agent target, incomplete verification |
| 403 | `lease_forbidden` | Critical dispatch without approval, DietCode marking done |
| 404 | `task_not_found` / `lease_not_found` | Unknown card or no active lease |
| 409 | `lease_conflict` | Illegal transition, duplicate lease, dispatch/Hermes failure after lease |

## Endpoints

### `POST /api/tasks/{id}/dispatch`

Starts DietCode execution: creates lease + handoff, then Hermes run.

**Body (optional):**

```json
{ "humanApprovedCritical": true }
```

**Critical cards** (`risk: 3`) return **403** unless `humanApprovedCritical` is `true`. Only one critical lease may occupy the global slot (including approved `leased`).

**Success:** `202 Accepted` with execution session payload.

**Dispatch failure** (Hermes unreachable): lease moves to `blocked` with `dispatch.failed` evidence; response **409** `lease_conflict`.

### `GET /api/tasks/{id}/lease`

Returns the active lease, or **404** `lease_not_found`.

### `POST /api/tasks/{id}/lease/agent-status`

DietCode transitions (not task column moves).

**Body:**

```json
{ "status": 2, "reason": "Build failed on net8 target" }
```

| `status` | Name | Notes |
|----------|------|--------|
| 2 | `blocked` | `reason` required |
| 3 | `verifying` | From `running` or `blocked` |

Agent cannot set `ready_for_review` (4) directly — use verification.

### `POST /api/tasks/{id}/verification`

**Body:**

```json
{
  "supersede": false,
  "report": {
    "cardId": "…",
    "sessionId": "…",
    "commandsRun": [
      { "command": "dotnet build", "passed": true, "summary": "0 errors" }
    ],
    "readyForHumanReview": true
  }
}
```

- All commands must pass **and** `readyForHumanReview: true` → lease `ready_for_review` (4).
- Any failure → lease stays `verifying` (3); failed report stored separately.
- Empty `commandsRun` → **400**.
- Replacing an existing passing report without `supersede: true` → **409**.

`supersede: true` appends `verification.superseded` evidence.

### `POST /api/tasks/{id}/lease/revoke`

**Body (optional):** `{ "reason": "Operator stopped run" }`

Sets lease `revoked` (5). Preserves worktree path and verification JSON in evidence.

### `POST /api/tasks/{id}/lease/agent-evidence`

Append-only evidence from the agent harness (e.g. `agent.heartbeat`, `agent.verify`).

**Body:**

```json
{ "kind": "agent.verify", "summary": "2/2 commands passed", "detail": { } }
```

**Success:** `200` with updated lease.

### `POST /api/tasks/{id}/lease/fail`

Operator-recorded failure with status-appropriate evidence:

| Lease status | Effect |
|--------------|--------|
| `leased` | `dispatch.failed` via `RecordDispatchFailureAsync` |
| `running` | `execution.failed` |
| `verifying` | Transition to `blocked` with reason |

**Body (optional):** `{ "reason": "..." }`

Other statuses → **409** `lease_conflict`.

### `POST /api/tasks/{id}/lease/heartbeat`

Refreshes `LastHeartbeatAt` for the active lease. Only the **assigned operator session** (or system recovery) may call this.

**Success:** `200` with updated lease.

Stale heartbeats (per status + risk thresholds in `LeaseRuntimeOptions`) cause the next lease touch to expire the lease to `blocked` or `revoked` with evidence — worktree paths are never deleted.

### `POST /api/tasks/{id}/lease/recover`

Human/system recovery flow (`RecoveryFlow: true` on the server). Does not allow cross-session mutation without recovery.

**Body:**

```json
{
  "mode": 0,
  "humanApprovedCritical": true,
  "reason": "Re-open after dispatch failure"
}
```

| `mode` | Name | Behavior |
|--------|------|----------|
| 0 | `ReopenBlocked` | `blocked` → `leased`, same worktree, evidence `recovery.reopen` |
| 1 | `ReattachWorktree` | Rebind existing worktree path on non-terminal lease |
| 2 | `ReplacementLease` | Revoke prior active lease (`recovery.superseded`), create new lease |

Critical recovery with `humanApprovedCritical: true` grants a **new** approval; consumed grants are never recycled on retry.

### `POST /api/tasks/{id}/lease/dispatch-retry`

Explicit dispatch retry after a blocked/failed path. Appends `dispatch.retry` evidence (distinct from first `dispatch.attempted`).

**Body:**

```json
{ "humanApprovedCritical": true }
```

Critical cards require `humanApprovedCritical: true` on **every** retry, even if a prior grant is still on the lease record.

### `POST /api/tasks/{id}/lease/merge`

Human-only. Requires lease `ready_for_review` with a passing verification report.

**Success:** `200` with updated task (`status: 6` Complete). This is the **only** path to Complete.

### `PUT /api/tasks/{id}/status`

**Body:** `{ "status": 3, "actor": 1 }` — `actor`: `0` Human, `1` DietCode, `2` System.

DietCode cannot set `Complete` → **403**.

## Status transition examples

```
leased (0) ──dispatch OK──► running (1)
running ──agent──► verifying (3) ──passing verification──► ready_for_review (4) ──merge──► merged (6)
running ──agent + reason──► blocked (2)
leased ──dispatch fail──► blocked (2)
any active ──revoke──► revoked (5) [terminal]
```

## Critical-card dispatch example

```bash
# 1. Create session + critical task (via API or UI)
# 2. Dispatch with approval
curl -s -X POST http://127.0.0.1:9470/api/tasks/{TASK_ID}/dispatch \
  -H 'Content-Type: application/json' \
  -d '{"humanApprovedCritical":true}'
```

Second critical card while the first is active → **409**.

## Verification submission example

```bash
curl -s -X POST http://127.0.0.1:9470/api/tasks/{TASK_ID}/verification \
  -H 'Content-Type: application/json' \
  -d '{
    "supersede": false,
    "report": {
      "cardId": "{TASK_ID}",
      "sessionId": "{SESSION_ID}",
      "commandsRun": [
        {"command":"dotnet build","passed":true,"summary":"ok"},
        {"command":"dotnet test","passed":true,"summary":"42 passed"}
      ],
      "readyForHumanReview": true
    }
  }'
```

Then merge:

```bash
curl -s -X POST http://127.0.0.1:9470/api/tasks/{TASK_ID}/lease/merge
```

## Runtime scheduler (Phase 24)

Configured under `LeaseRuntime` in app settings (see `LeaseRuntimeOptions`):

| Setting | Default role |
|---------|----------------|
| `MaxGlobalActiveLeases` | Reject new leases when at capacity (**409**) |
| `MaxActiveLeasesPerSession` | Per-operator-session cap |
| `MaxCriticalLeases` | Global critical slot (typically `1`) |
| `Duration.*Hours` | `ExpiresAt` by risk (critical shortest) |
| `Stale.*Minutes` | Heartbeat staleness for `leased` / `running` / `verifying` |
| `AbsoluteExpirationRevokes` | Past `ExpiresAt` → `revoked` instead of `blocked` |

Background `LeaseReconciliationHostedService` periodically:

- Expires stale active leases
- Blocks orphaned `running` leases whose execution already completed
- Blocks leases with missing worktree directories
- Blocks merged tasks that still hold an active lease

Evidence kinds include: `lease.expired_blocked`, `lease.expired_revoked`, `dispatch.retry`, `dispatch.failed`, `execution.failed`, `verification.failed`, `recovery.*`, `reconciliation.*`.

## Desktop client

`ControlPlaneClient` exposes typed helpers: `DispatchTaskDetailedAsync`, `GetLeaseDetailedAsync`, `SubmitVerificationDetailedAsync`, `MergeLeaseDetailedAsync`, etc. Failures return `ApiCallResult<T>` with `OperatorApiHints` next-step text for the kanban hint line.
