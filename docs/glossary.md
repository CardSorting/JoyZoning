# Glossary

Terms used across JoyZoning docs, UI, and APIs. For the product story behind these terms, see [concepts.md](concepts.md).

| Term | Meaning |
|------|---------|
| **Control plane** | `JoyZoning.ControlPlane` — local ASP.NET Core service on port **9470**; owns SQLite state, REST, SignalR. |
| **Operator session** | A workspace scope: name, `workspaceRoot`, optional Hermes profile. Kanban tasks belong to a session. |
| **Work task** | Kanban card in JoyZoning (`WorkTask`). Maps to Hermes kanban when sync is enabled. |
| **Execution session** | Tracks a single DietCode/Hermes **run** linked to a task (`ExecutionSession`). |
| **Execution lease** | Bounded authority to work on one card: worktree path, status, handoff, verification, evidence log. |
| **Handoff packet** | JSON prompt package for the executor: objective, acceptance criteria, verification commands. |
| **Worktree** | Isolated git directory under `<workspace>/.joyzoning/worktrees/<task-id>/`. |
| **Manager** | Hermes agent role for planning (Manager Chat); not a separate install. |
| **Executor / DietCode** | Worker agent role for implementation runs (Execution viewport). |
| **diet-hermes** | Local [Hermes Agent](https://github.com/NousResearch/hermes-agent) checkout; single install for both roles. |
| **Dispatch** | `POST /api/tasks/{id}/dispatch` — creates lease + handoff, starts Hermes run. |
| **Merge** | Human-only `POST .../lease/merge` after passing verification → task **Complete**. |
| **Critical card** | Task with `risk: 3`; requires `humanApprovedCritical` on dispatch/retry; global cap on concurrent critical leases. |
| **Evidence log** | Append-only JSON on the lease recording transitions, failures, agent actions. |
| **joy_events** | Append-only audit table; replay via `GET /api/events`, live via SignalR `OnJoyEvent`. |
| **jz** | Human operator CLI — dispatch, verify, merge, recovery. |
| **jz agent** | Constrained worker CLI inside a worktree — cannot merge or complete tasks. |
| **context.json** | `.joyzoning/context.json` in worktree — task/lease ids, allowed/forbidden actions for agents. |
| **Dashboard token** | Session token from `hermes dashboard` — required for kanban plugin API and TUI PTY WebSocket. |
| **Profile `joyzoning`** | Hermes config profile JoyZoning enables (API server on **8642**). |

## Status enums (quick reference)

**Kanban column (`WorkTaskStatus`):** Backlog → Planned → In Progress → Needs Approval → Verifying → Blocked → Complete.

**Lease (`ExecutionLeaseStatus`):** Leased → Running → Verifying → ReadyForReview → Merged; or Blocked / Revoked on failure paths.

See [control-plane-api.md](control-plane-api.md) for numeric values.
