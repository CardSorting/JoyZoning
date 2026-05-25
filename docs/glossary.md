# Glossary

Terms used across JoyZoning docs, UI, and APIs. For the product story, see [philosophy.md](philosophy.md) and [concepts.md](concepts.md). For non-technical wording, see [onboarding/plain-language-glossary.md](onboarding/plain-language-glossary.md).

| Term | Meaning |
|------|---------|
| **Control plane** | `JoyZoning.ControlPlane` — local ASP.NET Core service on port **9470**; owns SQLite state, REST, SignalR. |
| **Operator session** | A workspace scope: name, `workspaceRoot`, optional Hermes profile. Kanban tasks belong to a session. |
| **Work task** | Kanban card in JoyZoning (`WorkTask`). Maps to Hermes kanban when sync is enabled. |
| **Execution session** | Tracks a single DietCode/Hermes **run** linked to a task (`ExecutionSession`). |
| **Execution lease** | Bounded authority to work on one card: workspace path, card branch, status, handoff, verification, evidence log. |
| **Handoff packet** | JSON prompt package for the executor: objective, acceptance criteria, verification commands. |
| **Session workspace** | `OperatorSession.WorkspaceRoot` — the project folder opened via **Project → Open Workspace**. |
| **Canonical workspace** | `OperatorSession.WorkspaceRoot` — the real project folder; JSDP and leases execute here, not in sandboxes. |
| **Card branch** | `joyzoning/card-<task-id>` — git branch for supervised Hermes executor work in the canonical workspace. |
| **JSDP** | JoyZoning Sequential Delivery Protocol — sequential roles, shared workspace, mandatory accept-merge between roles. See [jsdp.md](jsdp.md). |
| **TaskExecutionMode** | `ManagedAgent` (Hermes lease) or `ExternalAgent` (no lease; Cursor/Claude/manual). |
| **ExecutionDriver** | Who runs work: `Hermes`, `ExternalCursor`, `ExternalClaudeCode`, `ExternalCopilot`, `ExternalManual`, etc. |
| **External-agent JSDP** | JSDP role executed without a Hermes lease; JoyZoning owns branch, prompt, verify, merge. See [external-agent-jsdp.md](external-agent-jsdp.md). |
| **1:1 workspace state** | One selected task resolves to one inspection path (and branch); Workspace, git porcelain, and timeline agree. See [workspace-state.md](workspace-state.md). |
| **WorkspaceInspection** | Control-plane resolver: task id → workspace root + card branch when leased. |
| **Manager** | Hermes agent role for planning (Manager Chat); not a separate install. |
| **Executor / DietCode** | Worker agent role for implementation runs (Execution viewport); Hermes executes, habitat supervises. |
| **diet-hermes** | Local [Hermes Agent](https://github.com/NousResearch/hermes-agent) checkout — **runtime authority** (journal, tools, convergence). |
| **Request managed run** | Operator action; API route remains `POST /api/tasks/{id}/dispatch` — creates lease + handoff, starts Hermes run (managed only). JoyZoning does not execute tools. |
| **Runtime owner** | Hermes — operational state in `~/.hermes/joyzoning/journal.db`. |
| **Habitat role** | JoyZoning — observe-only mirror, operator merge, leases; ingest rejects `authoritative: true`. |
| **External start** | `POST /api/tasks/{id}/external/start` or `jz task start-external` — branch + prompt, no lease. |
| **Merge** | Human-only accept-merge after verification → task **Complete** (`jz task complete --yes`; managed uses lease merge, external uses external complete). |
| **Critical card** | Task with `risk: 3`; requires `humanApprovedCritical` on managed-run request/retry; global cap on concurrent critical leases. |
| **Evidence log** | Append-only JSON on the lease recording transitions, failures, agent actions. |
| **joy_events** | Append-only audit table; replay via `GET /api/events`, live via SignalR `OnJoyEvent`. |
| **jz** | Human operator CLI — request runs, verify, merge, recovery. |
| **jz agent** | Constrained worker CLI in the lease workspace — cannot merge or complete tasks. |
| **context.json** | `.joyzoning/context.json` in the workspace — task/lease ids, allowed/forbidden actions for agents. |
| **Dashboard token** | Session token from `hermes dashboard` — required for kanban plugin API and TUI PTY WebSocket. |
| **Profile `joyzoning`** | Hermes config profile JoyZoning enables (API server on **8642**). |

## Framework terms (v1.6 — see [whitepaper-summary.md](whitepaper-summary.md))

| Term | Meaning |
|------|---------|
| **C1–C7** | Core invariants in the framework paper (asymmetry, acceptance/delivery, reviewability, informational debt, local/global comprehensibility, correctness vs comprehensibility, stable coordinates). |
| **Constraint persistence** | Observed: mutation substrates churn faster than inspectable acceptance disciplines (§1.3; not a separate invariant). |
| **B1–B4** | Boundary conditions (tests ≠ stabilization; narrative ≠ acceptance; etc.). |
| **Informational debt** | Cumulative cost of maintaining an accurate accepted-vs-proposed model when propose-rate exceeds reviewability. |
| **Stable review surface** | Fixed coordinates: unit → branch → diff → evidence → acceptance (maps to 1:1 card workspace in product). |
| **Merge authority** | Operator-only transition from proposed to accepted state (**C2**). |

## Status enums (quick reference)

**Kanban column (`WorkTaskStatus`):** Backlog → Planned → In Progress → … → Complete. External work adds **ExternalInProgress**, **ReadyForReview**, **Verified** on the task (no lease row).

**Lease (`ExecutionLeaseStatus`, managed only):** Leased → Running → Verifying → ReadyForReview → Merged; or Blocked / Revoked on failure paths. External tasks: use task status + `ExternalMergeCompleted`, not lease `Merged`.

See [control-plane-api.md](control-plane-api.md) for numeric values.
