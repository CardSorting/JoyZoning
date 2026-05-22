# Desktop UI guide

JoyZoning.App is an **Avalonia 12** operator console — not an IDE. It supervises Hermes manager runs and DietCode executor runs through the local control plane.

## Six product surfaces

| Surface | Purpose | Primary ViewModel |
|---------|---------|-------------------|
| **Getting Started** | Onboarding hub, health grade, quick actions | `OnboardingHubViewModel` |
| **Manager Chat** | Hermes project lead (streaming SSE → UI) | `ManagerChatViewModel` |
| **Kanban** | Task board, dispatch, lease actions | `KanbanViewModel` |
| **Execution** | DietCode run steps, terminal, Hermes TUI | `ExecutionViewportViewModel` |
| **Workspace** | File tree, changed files, diff preview | `WorkspaceViewModel` |
| **Approvals** | Risky Hermes tool approval inbox | `ApprovalsViewModel` |
| **Timeline** | Append-only event stream + JSON inspector | `TimelineViewModel` |

Getting Started is the default landing surface until core checklist steps complete (configurable; can reset via Settings).

## Status bar and Hermes chips

- **API** — Hermes API health (`GET /api/hermes/health`)
- **Dashboard** — reachability + token (`GET /api/hermes/dashboard`)

Click chips to open **Hermes → Connection…**. Use **Hermes → Ensure Gateway** when API is down.

## Manager Chat

- Sends messages via `POST /api/manager/message`; deltas arrive on SignalR (`OnJoyEvent` / stream handling in VM).
- **Parse** — extracts bullet/checkbox/numbered lines from the last assistant reply.
- **→ Task** — creates one DietCode task from the full last reply body.
- Empty state shows contextual nudges (workspace missing, connect Hermes).

Manager is **not** a code editor — use Workspace + external IDE for file edits.

## Kanban

- Columns map to `WorkTaskStatus`: Backlog → Planned → In Progress → Needs Approval → Verifying → Blocked → Complete.
- **New Task** dialog: title, description, agent (Hermes / DietCode), risk level.
- **Drag-and-drop** between columns updates status via `PUT /api/tasks/{id}/status`.
- **Dispatch** — `POST /api/tasks/{id}/dispatch`; creates execution lease + Hermes run.
- **Critical** tasks (`risk` 3): checkbox for human approval before dispatch.
- **Revoke lease**, **merge** (Complete column when `ready_for_review`), hint line from `OperatorApiHints` on API errors.
- **Import from Hermes** — `POST /api/tasks/import-kanban`; optional **auto-sync** in Settings.

## Execution viewport

- Shows execution steps and live terminal preview from Hermes tool events.
- **Hermes TUI** — `TerminalControl` from [SvcSystems.UI.Terminal](https://www.nuget.org/packages/SvcSystems.UI.Terminal) over dashboard WebSocket `/api/pty`.
- **Connect dashboard & TUI** — ensures dashboard, acquires token, opens PTY (resize via `\x1b[RESIZE:cols;rows]`).
- Contextual nudge when dashboard not connected.

## Workspace

- `GET /api/workspace/tree` — file tree for active workspace root.
- `GET /api/workspace/changed` — git changed files.
- `GET /api/workspace/diff` — unified diff; UI splits removed (red) / added (green) hunks.
- Line-numbered preview when diff unavailable.

## Approvals

- Lists `GET /api/approvals/pending`.
- Resolve with scope: **Once**, **Task**, **Session**, **Deny** → `POST /api/approvals/{id}/resolve`.
- Task-level grants stored in `approval_grants` auto-approve matching future requests.

## Timeline

- Loads `GET /api/events` with replay cursor; live feed via `OnJoyEvent`.
- Select row → JSON payload pane for debugging and audit.

## Onboarding hub (Phase 17–18)

**Written guide:** [onboarding/README.md](onboarding/README.md) · [setup checklist](onboarding/setup-checklist.md) · [status indicators](onboarding/status-indicators.md)

Patterns borrowed from VS Code Welcome / Linear checklists:

- **Journey phases** with numbered steps, time estimates, “why it matters” copy
- **Health grade**: Healthy / Degraded / Blocked
- **Quick actions** strip (Raycast-style shortcuts)
- **Troubleshooting playbooks** with Fix actions
- **Copy health report** for support
- **Optional milestones**: first manager message, first dispatch, TUI connect
- **Surface coach marks** — dismissible tips on Manager, Kanban, Execution, Workspace, Timeline
- **Smart setup** — same pipeline as launch auto-setup (`OnboardingAutoSetup`)

Menu: **Settings → Open Getting Started**, **Copy health report**, **Reset onboarding** (clears `onboarding.json`).

## Hermes Connection window

Advanced path when auto-setup did not finish:

1. Validate install root (Browse + path colors)
2. Checklist: gateway, API, dashboard, workspace
3. **Test full connection**
4. **Connect dashboard** / **Connect all**

Token override under Advanced (prefer automatic scrape).

## Setup wizard (legacy / advanced)

Four-step wizard still available: welcome → paths → connect → workspace. Auto-setup on launch usually makes this unnecessary.

## Recovery

On control plane restart, in-flight executions are marked **Interrupted**:

- Startup prompt or **Recovery** menu
- `POST /api/executions/{id}/resume` or `cancel`

Separate from **lease recovery** (`POST .../lease/recover`) used for blocked dispatch / stale workers.

## Settings

- Hermes URLs, profile, dashboard token, kanban auto-sync interval
- **Auto-connect on startup**
- Changes apply immediately to in-process Hermes HTTP clients (no control plane restart)

## Project menu

- **Open Workspace** — folder picker; creates/uses operator session for that root
- **Open last** — from onboarding remembered path

## Auto-start control plane

If nothing listens on `:9470`, the desktop app spawns `JoyZoning.ControlPlane` as a child process before connecting SignalR.

## What the desktop does not do

- Replace your IDE (no LSP, no full editor)
- Run without local Hermes (cloud agents out of scope for MVP)
- Let agents mark tasks **Complete** without human merge

For terminal automation, use [cli.md](cli.md).
