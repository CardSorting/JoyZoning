# JoyZoning Event Catalog

All events are stored in `joy_events` with monotonic `Id` (replay cursor), `CorrelationId` (usually task or session GUID), `Source`, `Type`, and `PayloadJson`.

## Sources

| Source | Origin |
|--------|--------|
| `JoyZoning` | Control plane orchestration |
| `Hermes` | Manager runs via SSE |
| `DietCode` | Executor runs via SSE |
| `Terminal` | Local PTY output (future) |
| `Git` | Repository status (future) |
| `Workspace` | File watcher |

## Canonical event types

### Session

| Type | When |
|------|------|
| `session.started` | Operator opens a project workspace |
| `session.ended` | Session closed |

### Tasks

| Type | When |
|------|------|
| `task.created` | New work task on board |
| `task.status_changed` | Kanban column transition |

### Hermes runs

| Type | When |
|------|------|
| `hermes.run.started` | Manager message dispatched |
| `hermes.run.completed` | Run finished |
| `hermes.message.delta` | Streaming assistant text |
| `hermes.tool.started` | Tool invocation begins |
| `hermes.tool.completed` | Tool finished |
| `hermes.approval.requested` | Runtime approval gate |
| `hermes.approval.resolved` | Operator resolved approval |

### DietCode execution

| Type | When |
|------|------|
| `dietcode.execution.started` | Task dispatched to executor |
| `dietcode.execution.completed` | Run reached terminal state |

### Workspace / terminal

| Type | When |
|------|------|
| `terminal.output` | Chunk of PTY output |
| `workspace.file.changed` | File modified during execution |
| `git.status.changed` | Working tree delta |

### Approvals (JoyZoning envelope)

| Type | When |
|------|------|
| `approval.granted` | Human approved (once / task / session) |
| `approval.denied` | Human denied |

## Replay API

```
GET /api/events?since={cursor}&correlationId={guid}&types=task.created,hermes.tool.completed
```

Response: ordered array of events with `Id > since`.

## SignalR push

Hub: `/hubs/operator`

Server → client:

- `OnJoyEvent` — every ingested event
- `OnTaskChanged` — kanban card update
- `OnExecutionUpdated` — execution session phase change
- `OnApprovalRequested` — new pending approval
- `OnTerminalOutput` — Hermes tool output preview (execution viewport)
- `OnKanbanSynced` — background kanban auto-import finished

## Example sequence

1. `session.started`
2. `hermes.run.started` (manager planning)
3. `task.created` × N
4. `task.status_changed` → InProgress
5. `dietcode.execution.started`
6. `hermes.tool.started` / `hermes.tool.completed`
7. `hermes.approval.requested`
8. `approval.granted`
9. `hermes.approval.resolved`
10. `dietcode.execution.completed`
11. `task.status_changed` → Complete
