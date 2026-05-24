# JoyZoning MVP Roadmap

## Phase 0 — Scaffold (complete)

- [x] Solution structure (6 projects)
- [x] Domain models and enums
- [x] SQLite schema + repositories
- [x] Control plane REST + SignalR hub
- [x] Hermes / DietCode adapters (real HTTP; Testing env uses deterministic stubs)
- [x] Avalonia shell with six surfaces
- [x] Architecture documentation

## Phase 1 — Hermes connectivity (complete)

- [x] `HermesProcessService` auto-start + `GET/POST /api/hermes/health|ensure`
- [x] Manager chat streams SSE deltas via SignalR
- [x] SignalR client in `JoyZoning.App`
- [x] Health indicator from Hermes API

## Phase 2 — Kanban + tasks (complete)

- [x] Load tasks from `GET /api/tasks`
- [x] Status updates via `PUT /api/tasks/{id}/status`
- [x] Sync to diet-hermes `/api/plugins/kanban/` when dashboard token configured
- [x] Tasks refresh on `OnTaskChanged` SignalR events
- [x] Project picker — **Project → Open Workspace**
- [x] **New Task** button on Kanban board

## Phase 3 — DietCode execution viewport (complete)

- [x] `POST /api/tasks/{id}/dispatch` from Kanban UI
- [x] Tool events → execution steps + terminal panel
- [x] Live terminal output via `OnTerminalOutput` SignalR (Hermes tool previews)
- [x] Workspace tree/changed files via control plane API

## Phase 4 — Approvals + safety (complete)

- [x] Approve once / session / task / deny
- [x] Risk classifier (heuristic)
- [x] Task-level grants (`approval_grants` table) — auto-approve matching requests

## Phase 5 — Recovery + polish (complete)

- [x] Interrupted runs marked on control plane startup
- [x] **Recovery** menu + startup prompt for interrupted executions
- [x] Resume / dismiss via `POST /api/executions/{id}/resume|cancel`
- [x] Event timeline loads history + live SignalR feed
- [x] **Settings** UI — Hermes URLs, profile, dashboard token for kanban sync

## Configuration notes

### Kanban sync & Hermes TUI

1. **Hermes → Connection…** → **Connect dashboard** (or **Connect all**).
2. JoyZoning starts the dashboard if needed, scrapes the session token, and saves it.
3. Kanban import/sync and **Execution → Hermes TUI** work without manual token paste.

Advanced: manual token override under **Connection → Advanced** or **Settings → Advanced**.

## Running

```bash
./scripts/run-dev.sh
# or separately:
dotnet run --project src/JoyZoning.ControlPlane
dotnet run --project src/JoyZoning.App
```

Ensure diet-hermes API server is on port **8642** for live agent runs. Operator docs: [README.md](README.md), [getting-started.md](getting-started.md).

## Phase 6 — Operator polish (complete)

- [x] **Runtime config reload** — saving Settings updates `HermesRuntimeSettings` + HTTP client without control plane restart
- [x] **Auto-start control plane** — desktop app spawns `JoyZoning.ControlPlane` if `:9470` is down
- [x] **Create Task dialog** — title, description, agent picker (Hermes / DietCode)
- [x] **Kanban drag-and-drop** — drag cards onto columns to change status
- [x] **Timeline payload inspector** — select event → JSON payload pane
- [x] **Workspace file preview** — line-numbered preview for changed files

## Phase 7 — Sync + operator workflows (complete)

- [x] **Import from Hermes kanban** — `POST /api/tasks/import-kanban` pulls `/api/plugins/kanban/board` (dashboard token required)
- [x] **Create task from manager reply** — **→ Task** button uses Hermes's last streamed response as DietCode task body
- [x] **Git diff preview** — `GET /api/workspace/diff` shows `git diff` for changed files (falls back to file preview)

## Phase 8 — Terminal + structured workflows (complete)

- [x] **Hermes TUI PTY attach** — WebSocket to dashboard `/api/pty` (superseded by Phase 11 VT100 widget)
- [x] **Hermes → Parse tasks** — bullet/checkbox/numbered lists from manager reply → create one or all
- [x] **Split diff view** — removed (red) / added (green) panes when `git diff` has hunks
- [x] Menu **Hermes → Open TUI Terminal** shortcuts to connect

## Phase 9 — Distribution + live sync (complete)

- [x] **Kanban auto-sync** — background import from Hermes board (Settings: enable + interval)
- [x] **OnKanbanSynced** SignalR — kanban refreshes when auto-sync completes
- [x] `GET /api/kanban/sync-status` — last sync time + message
- [x] `scripts/publish-macos.sh` — Release publish for macOS arm64
- [x] `scripts/run-dev.sh` — one-command dev launcher

## Phase 10 — Two-way kanban + macOS bundle (complete)

- [x] **Two-way kanban sync** — pull from Hermes board, then push local-only tasks + all linked statuses
- [x] **Local-wins on pull** — tasks edited in JoyZoning since last sync keep their status when pulling
- [x] **Dispatch pushes status** — moving a task to In Progress updates Hermes kanban
- [x] `scripts/bundle-macos-app.sh` — wraps publish output as `JoyZoning.app`
- [x] `.env.example` — documented Hermes connection variables

## Phase 11 — VT100 terminal widget (superseded by Phase 12)

- [x] Custom `HermesTerminalControl` + XTerm.NET (net6 era)

## Phase 12 — .NET 8 + SvcSystems.UI.Terminal (complete)

- [x] Solution target **`net8.0`** (`global.json` pins SDK 8.0.421)
- [x] **Avalonia 12** + **`SvcSystems.UI.Terminal`** (`TerminalControl` / `TerminalControlModel`)
- [x] `ReflowOnResize = false` for stable Hermes TUI during window resize
- [x] `Colors.axaml` included in `App.axaml`
- [x] Raw PTY bytes + `\x1b[RESIZE:cols;rows]` via `SizeChanged` on the model

## Phase 18 — Onboarding ops & activation (complete)

- [x] **Health grade** (Healthy / Degraded / Blocked) — status-page pattern
- [x] **Copy health report** — clipboard export for support (Docker Desktop / VS Code report issue)
- [x] **Step completion timestamps** — "Completed 2h ago" on checklist rows
- [x] **Quick actions** strip — Raycast-style shortcuts on the hub
- [x] **Troubleshooting playbooks** — expandable runbooks with Fix actions
- [x] **Learn** resource links — docs + Hermes agent repo
- [x] **Auto-track optional milestones** — first Manager Chat message, first kanban dispatch, TUI connect
- [x] **Prerequisite labels** on blocked steps (Linear dependency UX)
- [x] **Highlighted next step** — primary CTA step bordered on hub
- [x] Settings: **Open Getting Started**, **Copy health report**

## Phase 17 — Industry-standard onboarding hub (complete)

- [x] **Getting Started hub** (VS Code Welcome / Linear checklist pattern) — journey phases, numbered steps, why-it-matters copy, time estimates
- [x] **Smart setup** — one-click path → gateway → dashboard (Docker Desktop–style health flow)
- [x] **Diagnostics panel** — severity-coded issues with Fix actions
- [x] **Optional operate milestones** + **What's next** cards after core setup
- [x] **Surface coach marks** — dismissible first-visit tips on Manager, Kanban, Execution, Workspace, Timeline
- [x] **OnboardingCoordinator** + expanded preferences (schema v2, surface tips, celebration, last workspace)
- [x] Default landing on **Getting Started** until core checklist complete

## Phase 27 — Dogfood validation (complete)

- [x] Six automated paths in `DogfoodValidationTests` (happy, verify-fail, critical, stale, recovery, guardrails)
- [x] Subprocess control plane + `jz` CLI against shared test DB (`scripts/dogfood-validate.sh`)
- [x] [docs/dogfood-report.md](dogfood-report.md) — commands, expected/actual, bugs fixed, sharp edges
- [x] macOS worktree path normalization; Testing agent stubs for dogfood server

## Phase 27 — Workspace observability (complete)

- [x] **Git porcelain** — `GitWorkspaceStatus` + `LocalWorkspaceAdapter` (mtime fallback only for non-git or failed git)
- [x] **Timeline ingestion** — `WorkspaceEventPublisher` → `git.status.changed` / `workspace.file.changed`; `HermesRunEventConsumer` persists `terminal.output`
- [x] **Task worktree APIs** — `GET /api/tasks/{id}/workspace/{changed,tree,diff}` via `WorkspaceInspection`
- [x] **Desktop + CLI** — lease worktree label, Refresh, SignalR-driven refresh (`OnWorktreeRefreshed`, execution/lease events)
- [x] **Workspace change publisher** — `WorkspaceEventPublisher` dedupes git porcelain; SignalR `OnWorktreeRefreshed` on task workspace poll
- [x] **Operator TUI** — `/workspace` uses task APIs when `/use <task>` is set; hub stream shows worktree updates
- [x] **Docs** — [workspace-state.md](workspace-state.md) (1:1 card → folder, PR/VS Code analogies, non-technical navigation)

## Phase 26 — Agent-safe harness (`jz agent`) (complete)

- [x] `jz agent` start / heartbeat / verify / blocked / done
- [x] `.joyzoning/context.json` in worktree (dispatch + agent start)
- [x] Guardrails: no merge/revoke/complete/raw; worktree enforcement
- [x] `agent.done` → ready_for_review only; evidence kinds on lease
- [x] Example scripts under `scripts/examples/`
- [x] CLI tests in `AgentHarnessTests.cs`

## Phase 25 — Operator CLI (`jz`) (complete)

- [x] Operator workflows: `task run`, `verify` (local `--cmd`), `complete`, `fail`, `recover`
- [x] Context inference: worktree cwd, `JOYZONING_SESSION_ID`, single active lease
- [x] Safety: `--yes`, `--approve-critical`, merge-only complete, `config explain`, `doctor`
- [x] `jz lease` / `heartbeat` / `verify` shortcuts; `completion bash`; CLI tests
- [x] [docs/cli.md](cli.md) — happy/failure/critical/automation recipes

## Phase 24 — Runtime scheduling + lease recovery (complete)

- [x] `LastHeartbeatAt`, ownership fields (`CreatedBy`, `AssignedSessionId`, `AssignedAgent`), `DispatchAttemptCount`, `RecoveredFromLeaseId`
- [x] `LeaseSchedulerPolicy` + `LeaseRuntimeOptions` (duration by risk, stale thresholds, global/session caps)
- [x] Heartbeat API + stale/absolute expiration → blocked/revoked with evidence (worktree preserved)
- [x] `RecoverLeaseAsync` (reopen blocked, reattach worktree, replacement lease)
- [x] Explicit `dispatch.retry` / `execution.failed` / `verification.failed` evidence kinds
- [x] Critical approval cleared on expiration; retry requires new grant when consumed
- [x] `LeaseReconciliationHostedService` (orphans, missing worktree, merged task + active lease)
- [x] Tests in `LeaseRuntimeServiceTests.cs`

## Phase 23 — API integration + desktop wiring (complete)

- [x] HTTP integration tests for dispatch, lease, agent-status, verification, revoke, merge (`OrchestrationApiIntegrationTests`)
- [x] Status codes: 400/403/404/409/202/200 verified on real `WebApplicationFactory` host
- [x] Critical dispatch, supersede, dispatch-failure recovery covered at API boundary
- [x] Desktop `ControlPlaneClient` + `ApiCallResult` / `OperatorApiHints` (no raw exceptions in UI hints)
- [x] Kanban: critical approval checkbox, revoke lease, merge-via-Complete move
- [x] API contract doc: `docs/execution-orchestration-api.md`

## Phase 22 — Execution lease hardening (complete)

- [x] Explicit lease transition matrix + `LeaseOrchestrationException` (400/403/404/409)
- [x] Structured append-only evidence log (actor, from/to, reason, summary)
- [x] Dispatch failure recovery (leased → blocked + evidence, no silent stuck state)
- [x] Critical approval grant/consume per lease; global critical slot includes approved leased
- [x] Worktree path sandbox under `.joyzoning/worktrees`
- [x] Verification integrity (structure, failed path, supersede guard)
- [x] SQLite partial unique index: one active lease per card
- [x] Extended tests in `tests/JoyZoning.Tests/`

## Phase 21 — Kanban execution orchestration (complete)

- [x] **ExecutionLease**, **HandoffPacket**, **VerificationReport** domain models
- [x] **`KanbanExecutionOrchestrator`** under existing kanban (no board rebuild)
- [x] Flow: card → lease → handoff → DietCode worktree → verification → human merge/revoke
- [x] Rules: one active lease per card; critical approval + global critical cap; agent cannot mark done
- [x] API: `/api/tasks/{id}/lease`, `lease/agent-status`, `verification`, `lease/revoke`, `lease/merge`
- [x] `DispatchTask` creates lease + handoff before Hermes run
- [x] Tests in `tests/JoyZoning.Tests/`

## Phase 20 — Single diet-hermes install (complete)

- [x] **`scripts/install-diet-hermes.sh`** — one checkout at `~/Downloads/diet-hermes-main-master` (no duplicate hermes-agent install)
- [x] **Manager vs executor** — same Hermes gateway; roles split across **sessions**, work synced via **kanban**
- [x] **`HermesStackInstaller`** — streams progress into setup overlay; skips rebuild when `.venv`/`venv` already has `hermes`
- [x] **`.venv` + `venv`** CLI discovery; canonical path preferred in `OnboardingPathDiscovery`
- [x] Auto-configure `joyzoning` profile: `API_SERVER_ENABLED`, port, generated `API_SERVER_KEY`

## Phase 19 — Zero-config onboarding (complete)

- [x] **`OnboardingAutoSetup`** — launch pipeline: control plane → auto-detect install root → gateway → dashboard → workspace
- [x] **Auto setup on launch** (`AutoSetupOnLaunch`, default on) — no blocking setup wizard on first run
- [x] **Full-screen setup overlay** — progress % + plain-language status while services start
- [x] **Sample workspace** at `~/Library/Application Support/JoyZoning/workspaces/getting-started` when no project is found
- [x] **Land on Manager Chat** when Hermes + workspace are ready; Getting Started only when setup is incomplete
- [x] **Smart setup** reuses the same pipeline (`force` retry from banner / hub)
- [x] Workspace picker fallback with **Use sample workspace** vs **Choose folder…**

## Phase 16 — Onboarding guidance (complete)

- [x] **Auto-detect** diet-hermes paths (Downloads, env vars, `.venv/bin/hermes` validation)
- [x] **Actionable checklist** — Fix buttons per incomplete step (gateway, dashboard, workspace)
- [x] **Sidebar setup card** — progress %, Connect / Open workspace shortcuts while incomplete
- [x] **Manager Chat empty state** — contextual nudge with workspace / connect actions
- [x] **Last workspace** — remembered path, **Open last** on workspace banner + wizard step
- [x] **Open Hermes TUI after wizard** optional checkbox

## Phase 16 — Hermes-aligned operator terminal (complete)

Strategy doc: [hermes-aligned-terminal-strategy.md](hermes-aligned-terminal-strategy.md).

- [x] **`jz tui`** — operator REPL: slash registry, tab completion, line history, multiline input (`\`, Ctrl+G → `$EDITOR`)
- [x] **Live event stream** — SignalR `OnJoyEvent` + manager deltas (`/watch`); tool lines via `hermes.tool.*`
- [x] **`jz hermes tui`** — delegate agent chat to `hermes --tui` (no second chat stack in .NET)
- [x] **TTY vs automation** — bare `jz` on TTY → operator TUI; `JOYZONING_NO_TUI=1` → JSON stdout
- [x] **`scripts/dotnet-env.sh`** — prefer `~/.dotnet` SDK 8 for builds (macOS PATH pitfall)

## Phase 15 — Onboarding depth (complete)

- [x] **`OnboardingEvaluator`** — install-path validation, 4-item checklist, setup % progress
- [x] **Setup wizard** — progress bar, live checklist, Browse for install root, path gate on Next, auto-advance after connect
- [x] **Setup banner** — dismiss (×), progress subtitle (`Setup 75% — …`), workspace follow-up banner when Hermes OK but no session
- [x] **Hermes Connection** — checklist + **Test full connection**, Browse install root, path validation colors
- [x] **Settings → Reset onboarding** — clears `~/Library/Application Support/JoyZoning/onboarding.json`
- [x] **Folder picker** helper for wizard, connection, and **Project → Open Workspace**

## Phase 14 — Onboarding polish (complete)

- [x] **First-run setup wizard** (4 steps: welcome, paths, connect, workspace)
- [x] **Setup banner** on main window when Hermes not fully ready
- [x] **Connect Hermes** one-shot menu + banner actions
- [x] **Auto-connect on startup** preference (wizard + Settings)
- [x] Kanban + Execution contextual nudges (banner, reconnect TUI)
- [x] Clickable API/Dashboard status → Connection window

## Phase 13 — Visual Hermes onboarding (complete)

- [x] **`HermesDashboardService`** — start `hermes dashboard --no-open --tui`, scrape token from HTML
- [x] **`POST /api/hermes/ensure-dashboard`** + **`GET /api/hermes/dashboard`** + refresh-token
- [x] **Hermes Connection** window (paths, ensure gateway, connect dashboard, step checklist)
- [x] Status bar **Dashboard:** chip; menu **Hermes → Connection…**
- [x] **Execution → Hermes TUI** connection card — **Connect dashboard & TUI** (auto token + PTY)
- [x] Settings simplified — connection summary + link; token under Advanced
