# Control plane REST API

Base URL: `http://127.0.0.1:9470` (override via `ControlPlane:ListenUrl` or `JOYZONING_URL` for CLI).

All JSON bodies use camelCase. Lease orchestration errors return structured JSON — see [execution-orchestration-api.md](execution-orchestration-api.md).

## Health

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/health` | `{ "status": "ok", "service": "joyzoning-control-plane" }` |

## Agent operations

Machine-readable self-description for coding agents. Full guide: [agent-operations.md](agent-operations.md).

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/agent/manifest` | Static manifest: commands, endpoints, verification, important files |
| `GET` | `/api/agent/context` | Runtime state: sessions, active/blocked leases, pending approvals |
| `GET` | `/api/agent/endpoints` | Endpoint registry; `?agentSafe=true` for agent-safe routes only |

`GET /api/watch/bootstrap` includes an `agentOps` block with URLs to the above.

## Sessions

| Method | Path | Body | Response |
|--------|------|------|----------|
| `GET` | `/api/sessions` | — | List operator sessions |
| `POST` | `/api/sessions` | `{ "name", "workspaceRoot", "hermesProfile"? }` | `201` + session |
| `GET` | `/api/sessions/{id}` | — | Session or `404` |

## Tasks (kanban cards)

| Method | Path | Body | Notes |
|--------|------|------|-------|
| `GET` | `/api/tasks?sessionId={guid}` | — | **Required** `sessionId` query |
| `POST` | `/api/tasks` | `{ "sessionId", "title", "description"?, "assignedAgent"?, "risk"? }` | `assignedAgent`: `0` Hermes, `1` DietCode; `risk`: `0`–`3` |
| `PUT` | `/api/tasks/{id}/status` | `{ "status", "actor"? }` | `actor`: `0` Human, `1` DietCode, `2` System; DietCode cannot set Complete |
| `POST` | `/api/tasks/import-kanban` | `{ "sessionId" }` | Pull from Hermes kanban plugin; pushes local changes back |

### Execution dispatch

| Method | Path | Body | Response |
|--------|------|------|----------|
| `POST` | `/api/tasks/{id}/dispatch` | `{ "humanApprovedCritical"?: bool }` | `202` + execution session; creates lease + handoff; critical (`risk` 3) needs `humanApprovedCritical: true` |

Lease-specific endpoints are documented in [execution-orchestration-api.md](execution-orchestration-api.md), including:

- `GET /api/tasks/{id}/lease`
- `POST .../lease/heartbeat`, `agent-status`, `agent-evidence`, `recover`, `fail`, `dispatch-retry`, `revoke`, `merge`
- `POST /api/tasks/{id}/verification`

### External execution (no lease)

For `TaskExecutionMode.ExternalAgent` — Cursor, Claude Code, manual. **No** `ExecutionLease` row is created; `GET /api/tasks/{id}/lease` returns `404` by design.

| Method | Path | Body | Notes |
|--------|------|------|-------|
| `POST` | `/api/tasks/{id}/external/start` | `{ "agent": "cursor" \| "claude-code" \| "copilot" \| "manual" }` | Creates card branch, stores prompt; status → `ExternalInProgress` |
| `GET` | `/api/tasks/{id}/external/prompt` | — | JSDP handoff text for the external tool |
| `GET` | `/api/tasks/{id}/external/status` | — | Driver, branch, scan summary |
| `GET` | `/api/tasks/{id}/workspace/status` | — | Git porcelain + `readyForReviewAllowed` / `blockedReason` |
| `POST` | `/api/tasks/{id}/external/ready-for-review` | — | Operator gate after edits (branch + diff checks) |
| `POST` | `/api/tasks/{id}/external/verify` | `{ "report": VerificationReport }` | External-only verify (same payload as `/verification`) |
| `POST` | `/api/tasks/{id}/external/complete` | `{ "operatorApproved": true }` | Accept-merge + `Complete` (same authority as managed merge) |

`POST /api/tasks/{id}/verification` also routes to the external flow when the task is external (preferred for `jz task verify`). Agents must **not** call `PUT /api/tasks/{id}/status` with `Complete`.

Guide: [external-agent-jsdp.md](external-agent-jsdp.md) · [execution-paths.md](execution-paths.md).

### Delivery chains (JSDP)

| Method | Path | Body | Notes |
|--------|------|------|-------|
| `POST` | `/api/delivery-chains` | Create chain payload | Eight bounded roles on one workspace |
| `GET` | `/api/delivery-chains/{id}/queue` | — | Merge gate + `blockReason` per role |
| `POST` | `/api/delivery-chains/{id}/next-external` | `{ "agent": "cursor" }` | Start next **eligible** role externally |
| `GET` | `/api/delivery-chains/{id}/prompt` | — | Prompt for active/next chain role |

CLI: `jz delivery-chain create \| queue \| next --external --agent cursor`. Protocol: [jsdp.md](jsdp.md).

## Manager chat

| Method | Path | Body | Response |
|--------|------|------|----------|
| `POST` | `/api/manager/message` | `{ "sessionId", "message" }` | `{ "runId" }` — streams via Hermes SSE → SignalR |

## Executions

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/executions/{id}` | Execution session by id |
| `GET` | `/api/executions/interrupted` | Runs marked interrupted on control plane restart |
| `POST` | `/api/executions/{id}/resume` | Resume tracking + Hermes run |
| `POST` | `/api/executions/{id}/cancel` | Dismiss / cancel interrupted run |

## Approvals

| Method | Path | Body |
|--------|------|------|
| `GET` | `/api/approvals/pending` | — |
| `POST` | `/api/approvals/{id}/resolve` | `{ "scope" }` — Once / Task / Session / Deny |

## Events (replay)

| Method | Path | Query |
|--------|------|-------|
| `GET` | `/api/events` | `since`, `correlationId`, `types` (comma-separated) |

See [event-catalog.md](event-catalog.md).

## Workspace (git-aware)

| Method | Path | Query |
|--------|------|-------|
| `GET` | `/api/workspace/tree` | `workspaceRoot` |
| `GET` | `/api/workspace/changed` | `workspaceRoot`, optional `sessionId` (timeline correlation) |
| `GET` | `/api/workspace/diff` | `workspaceRoot`, `path` |
| `GET` | `/api/tasks/{id}/workspace/changed` | Canonical workspace; card branch when dispatched |
| `GET` | `/api/tasks/{id}/workspace/tree` | Same resolution as above |
| `GET` | `/api/tasks/{id}/workspace/diff` | `path` — diff inside resolved worktree/session root |

**Workspace events:** `GET /api/tasks/{id}/workspace/changed` publishes git/workspace timeline events and broadcasts `OnWorktreeRefreshed` / `OnTaskLiveUpdated` when the snapshot hash changes.

## Hermes connectivity

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/hermes/health` | API reachability + `apiUrl` |
| `POST` | `/api/hermes/ensure` | Start gateway if configured |
| `GET` | `/api/hermes/dashboard` | Dashboard reachability + token state |
| `POST` | `/api/hermes/ensure-dashboard` | `{ "alsoEnsureGateway"?: bool }` — start dashboard, acquire token |
| `POST` | `/api/hermes/refresh-dashboard-token` | Re-scrape token |

## App config (persisted in SQLite)

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/config` | Merged settings (Hermes URLs, kanban auto-sync, display flags) |
| `PUT` | `/api/config` | Save settings; **hot-reloads** `HermesRuntimeSettings` without restart |

## Kanban sync status

| Method | Path | Response |
|--------|------|----------|
| `GET` | `/api/kanban/sync-status` | `{ "lastSyncAt", "message" }` |

Background `KanbanAutoSyncHostedService` runs when enabled in saved config.

## SignalR hub

**URL:** `/hubs/operator`

| Server event | When |
|--------------|------|
| `OnJoyEvent` | Any ingested `joy_events` row |
| `OnTaskChanged` | Task created or status updated |
| `OnExecutionUpdated` | Execution phase / steps changed |
| `OnApprovalRequested` | New pending approval |
| `OnTerminalOutput` | Hermes tool output preview |
| `OnKanbanSynced` | Auto-import completed |

CORS allows `http://127.0.0.1` and `http://localhost` with credentials.

## Enum quick reference

### `WorkTaskStatus` (kanban column)

| Value | Name |
|-------|------|
| 0 | Backlog |
| 1 | Planned |
| 2 | InProgress |
| 3 | NeedsApproval |
| 4 | Verifying |
| 5 | Blocked |
| 6 | Complete |
| 7 | ReadyToStart |
| 8 | HermesRunning |
| 9 | ExternalInProgress |
| 10 | ReadyForReview |
| 11 | Verified |
| 12 | Failed |

External JSDP commonly uses **9 → 10/11 → 6** after operator `external/complete`. Managed dispatch uses lease statuses below.

### `ExecutionLeaseStatus` (managed only)

| Value | Name |
|-------|------|
| 0 | Leased |
| 1 | Running |
| 2 | Blocked |
| 3 | Verifying |
| 4 | ReadyForReview |
| 5 | Revoked |
| 6 | Merged |

### `RiskLevel`

| Value | Name |
|-------|------|
| 0 | Low |
| 1 | Medium |
| 2 | High |
| 3 | Critical |

### `LeaseRecoveryMode`

| Value | Name |
|-------|------|
| 0 | ReopenBlocked |
| 1 | ReattachWorktree |
| 2 | ReplacementLease |
