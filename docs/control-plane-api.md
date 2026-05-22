# Control plane REST API

Base URL: `http://127.0.0.1:9470` (override via `ControlPlane:ListenUrl` or `JOYZONING_URL` for CLI).

All JSON bodies use camelCase. Lease orchestration errors return structured JSON — see [execution-orchestration-api.md](execution-orchestration-api.md).

## Health

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/health` | `{ "status": "ok", "service": "joyzoning-control-plane" }` |

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
| `GET` | `/api/workspace/changed` | `workspaceRoot` |
| `GET` | `/api/workspace/diff` | `workspaceRoot`, `path` |

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

### `ExecutionLeaseStatus`

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
