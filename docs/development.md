# Development guide

**Product philosophy:** [philosophy.md](philosophy.md) — canonical workspace, JSDP, cognition vs authority.

Contributing: [CONTRIBUTING.md](../CONTRIBUTING.md) (MIT license, PR expectations).

## Repository layout

```
JoyZoning/
├── src/
│   ├── JoyZoning.Domain/          # Entities, enums, lease rules, orchestration DTOs
│   ├── JoyZoning.Persistence/     # EF Core + SQLite, repositories, migrations
│   ├── JoyZoning.Agents/          # Hermes / DietCode HTTP adapters (IAgentAdapter)
│   ├── JoyZoning.Adapters/        # Workspace file tree + git diff
│   ├── JoyZoning.ControlPlane/   # ASP.NET Core REST + SignalR + hosted services
│   ├── JoyZoning.Cli/             # `jz` operator CLI
│   └── JoyZoning.App/             # Avalonia 12 desktop UI
├── tests/
│   ├── JoyZoning.Tests/           # API + orchestrator + dogfood (not in .sln — run by path)
│   └── JoyZoning.Cli.Tests/       # CLI unit tests (in JoyZoning.sln)
├── broccoliq/                     # Vendored @noorm/broccoliq hive + joy-bridge worker (:9471)
├── scripts/                       # Dev, publish, install, examples, dogfood
├── docs/                          # Documentation
├── global.json                    # SDK 8.0.421
└── JoyZoning.sln                  # Core projects + JoyZoning.Cli.Tests
```

**Note:** `tests/JoyZoning.Tests` is referenced by CI-style scripts but is **not** listed in `JoyZoning.sln`. Run it explicitly:

```bash
dotnet test tests/JoyZoning.Tests/JoyZoning.Tests.csproj
```

## .NET SDK (required: 8.x)

`global.json` requires **8.0.x** (rollForward: latestFeature). On macOS, `/usr/local/share/dotnet` is often **.NET 6 only** while **.NET 8** is installed under `~/.dotnet` — `dotnet` in PATH then fails even though 8 is installed.

**Permanent fixes (pick one):**

| Approach | What it does |
|----------|----------------|
| **Shell profile** | Add to `~/.zshrc`: `export DOTNET_ROOT="$HOME/.dotnet"` and `export PATH="$DOTNET_ROOT:$PATH"` |
| **Repo shim** | `./scripts/dotnet test ...` — always uses `~/.dotnet` when 8.x is there |
| **Cursor / VS Code** | `.vscode/settings.json` sets `DOTNET_ROOT` for integrated terminals (reload window) |
| **direnv** | `direnv allow` in repo root (`.envrc`) |

```bash
./scripts/ensure-dotnet-sdk.sh   # verify or install 8.x to ~/.dotnet
source scripts/dotnet-env.sh     # same PATH fix for bare `dotnet`
dotnet --list-sdks               # should show 8.0.x
```

If no 8.x SDK is listed:

```bash
curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0
source scripts/dotnet-env.sh
```

## Build

```bash
source scripts/dotnet-env.sh
dotnet restore
dotnet build JoyZoning.sln
```

Single project:

```bash
dotnet build src/JoyZoning.ControlPlane/JoyZoning.ControlPlane.csproj
dotnet run --project src/JoyZoning.ControlPlane
dotnet run --project src/JoyZoning.App
```

## JSDP workspace progress (canonical checkout)

JoyZoning uses the **JoyZoning Sequential Delivery Protocol (JSDP)** by default: agents work in the **canonical session workspace** on branch `joyzoning/card-<id>`. There is no `.joyzoning/worktrees/` sandbox or `.joyzoning/live/` mirror copy.

| Mechanism | What you get |
|-----------|----------------|
| **`GET /api/tasks/{id}/workspace/changed`** | Git porcelain changed files for the task workspace |
| **`GET /api/tasks/{id}/workspace/diff`** | Unified diff for one path |
| **`GET /api/sessions/{id}/parallel-workers`** | Workers + merge state (`protocol: jsdp`, `workspacePath`) |
| **`jz task watch <id>`** | Polls workspace/changed + parallel-workers (CLI) |
| **Watch UI** | `useLiveTask` builds presentation from workspace polling + SignalR |
| **SignalR** | `OnWorktreeRefreshed`, `OnTaskLiveUpdated`, `OnTerminalOutput`, `OnCodeActivity` |

Terminal watcher:

```bash
jz task watch <task-id> --interval 5
jz task watch <task-id> --once
```

After accept-merge or revoke, legacy `.joyzoning/worktrees` and `.joyzoning/live` folders under the project are pruned automatically.

## Web progress dashboard (Watch UI — Next.js)

The Watch UI lives in **`web/watch/`** — **Next.js 15**, **React 19**, **TypeScript**, Tailwind, Framer Motion, SignalR. It uses familiar **package-tracking** and **deploy-dashboard** patterns for non-technical users.

### URLs

```text
http://127.0.0.1:9470/                              # production (static export in wwwroot)
http://127.0.0.1:9470/?taskId=<uuid>
http://localhost:3000/                              # local dev (next dev, proxies API)
```

### Build & publish

```bash
./scripts/build-watch-ui.sh    # npm run build → copies out/ to ControlPlane/wwwroot
```

### Dev workflow

```bash
# Terminal 1
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/JoyZoning.ControlPlane

# Terminal 2
cd web/watch && npm install && npm run dev
```

See [web/watch/README.md](../web/watch/README.md).

### Features

- **Welcome wizard** — pick workspace + task, or jump to in-progress builds
- **Status hero** — headline, progress ring, plain-language chips
- **Vertical journey** — Setup → Build → Verify → Review (“you are here”)
- **Right now** spotlight — typing indicator while active
- **Tabs** — Workshop (live code + files) · Checklist · Timeline
- **SignalR** — `OnTerminalOutput`, `OnCodeActivity`, `OnTaskLiveUpdated`, `OnWorktreeRefreshed`

**Throughput tuning** (`appsettings.Development.json`): stale lease thresholds, stream poll interval.

Bootstrap API: `GET /api/watch/bootstrap`.

**Operational modes:** see [operational-modes.md](operational-modes.md) — keep Planning (kanban), Execution (workers/mirrors), Review (merge queue), and Habitat (ambient watch) as separate metaphors in new UI/read models. Registry: `GET /api/operational-modes`.

Parallel workers (JSDP): `GET /api/sessions/{sessionId}/parallel-workers` (canonical `workspacePath`, merge readiness, authority). **Bounded session dispatch:** `GET /api/sessions/{sessionId}/delivery-plan` (`model: bounded_session`, one agent + one role per session) — see [bounded-session-audit.md](bounded-session-audit.md). Merge/reconciliation queue: `GET /api/sessions/{sessionId}/merge-queue` (buckets: ready to merge, conflicts, completed, revoked/abandoned; includes `mergeState`, `mergeReadiness`, `mergeConflict`, `decisionSummary`, `approveGuardrails`, `revokeGuardrails`). Operator preflight: `GET /api/sessions/{sessionId}/workers/{executionSessionId}/decision-preflight?action=accept|revoke|inspect` (`approve` aliases `accept`). Human actions: `POST /api/tasks/{taskId}/lease/merge`, `POST /api/tasks/{taskId}/lease/revoke`. Open mirror folder (macOS/Linux): `POST /api/sessions/open-path` with `{ "path": "..." }`. **Convergence model (worktree → main workspace, what accept does):** [worker-convergence.md](worker-convergence.md). **Bounded YOLO / authority autopilot:** [authority-autopilot.md](authority-autopilot.md). **Coherence audit (May 2026):** [bounded-yolo-coherence-audit.md](bounded-yolo-coherence-audit.md).

## Run (development)

| Command | What it does |
|---------|----------------|
| `./scripts/run-dev.sh` | Control plane (background) + desktop |
| `dotnet run --project src/JoyZoning.ControlPlane` | API + Watch UI on `:9470` |
| `dotnet run --project src/JoyZoning.App` | Desktop (auto-starts control plane) |
| `./scripts/jz doctor` | CLI smoke check without installing `jz` |
| `./scripts/broccoliq-build.sh` | Build vendored BroccoliQ + joy-bridge worker |
| `./scripts/broccoliq-verify.sh` | CI check: dist + worker present |

**BroccoliQ:** see [broccoliq.md](broccoliq.md) — event/task mirroring to `broccoliq.db` on `:9471`.

## Test

```bash
source scripts/dotnet-env.sh

# Default — fast tier only (unit tests, no API host, no dogfood)
./scripts/run-tests.sh
# or: ./scripts/run-tests.sh fast

# Heavier tiers (opt-in)
./scripts/run-tests.sh integration   # WebApplicationFactory API tests
./scripts/run-tests.sh medium        # unit + integration
./scripts/run-tests.sh dogfood       # subprocess server + real jz binary
./scripts/run-tests.sh all           # everything (slow)
```

Bare `dotnet test tests/JoyZoning.Tests/...` also defaults to **Category=Unit** only (see `VSTestTestCaseFilter` in the test csproj). CLI tests are always lightweight.

| Tier | Trait | What runs |
|------|--------|-----------|
| **fast** (default) | `Unit` | Domain/orchestrator rules, in-memory SQLite, CLI parsers |
| **integration** | `Integration` | Shared `WebApplicationFactory` host (`OrchestrationApi` collection, sequential) |
| **dogfood** | `Dogfood` | Real Kestrel port + `jz` subprocess |

Key test areas:

| File / area | Covers |
|-------------|--------|
| `OrchestrationApiIntegrationTests` | HTTP status codes for lease API |
| `KanbanExecutionOrchestratorTests` | Transition matrix, evidence |
| `LeaseRuntimeServiceTests` | Stale heartbeat, expiration, caps |
| `DogfoodValidationTests` | End-to-end `jz` / `jz agent` paths |
| `ExternalAgentJsdpTests` | External start → mark-ready → verify → complete (API host) |
| `ExternalAgentJsdpPolicyTests` | Merge gate, status transitions, bounded-session guards |
| `JsdpChainIntegrationTests` | Delivery chain queue + external next role |
| `JoyZoning.Cli.Tests` | Args, safety, JSON output, agent guard |

Dogfood uses `JoyZoningDogfoodServerProcess` (real Kestrel port) + shared temp SQLite — see [dogfood-report.md](dogfood-report.md).

**Orchestration API integration tests** (`OrchestrationApi` xUnit collection):

- Shared `WebApplicationFactory` + **in-memory SQLite** (`Mode=Memory;Cache=Shared`) — avoids disk I/O errors from `EnsureDeletedAsync` while the host holds connections open.
- `TestDatabaseReset.ClearAllAsync` deletes rows instead of dropping the database file between tests.
- `HermesRunEventConsumer.TrackRun` is a **no-op** in `Testing` so stub SSE reconciliation does not race lease assertions across test methods.
- Filtered run: `dotnet test tests/JoyZoning.Tests/JoyZoning.Tests.csproj --filter "FullyQualifiedName~OrchestrationApi"`.
- External JSDP: `dotnet test tests/JoyZoning.Tests/JoyZoning.Tests.csproj --filter "FullyQualifiedName~ExternalAgentJsdp|JsdpChainIntegration"`.

## Scripts

| Script | Purpose |
|--------|---------|
| `install-diet-hermes.sh` | Clone/install single diet-hermes tree + venv |
| `install-hermes-stack.sh` | Wrapper for stack setup |
| `install-jz.sh` | Publish CLI to `~/.local/bin/jz` |
| `publish-macos.sh` | Release `net8.0` osx-arm64 publish → `dist/` |
| `bundle-macos-app.sh` | Wrap publish as `JoyZoning.app` |
| `dogfood-validate.sh` | Build + filter dogfood tests |
| `examples/*.sh` | Documented operator/agent flows |

## Control plane internals (edit map)

| Area | Path |
|------|------|
| REST routes | `ControlPlane/Endpoints/ApiEndpoints.cs` |
| Task dispatch + kanban import | `ControlPlane/Services/OrchestrationService.cs` |
| Execution leases | `ControlPlane/Services/KanbanExecutionOrchestrator.cs` |
| Scheduler / stale / caps | `ControlPlane/Services/LeaseRuntimeService.cs` |
| Background reconcile | `ControlPlane/Background/LeaseReconciliationHostedService.cs` |
| Kanban auto-sync | `ControlPlane/Background/KanbanAutoSyncHostedService.cs` |
| SignalR | `ControlPlane/Hubs/OperatorHub.cs` |
| Hermes SSE consumer | `ControlPlane/Background/HermesRunEventConsumer.cs` |
| Testing stubs | `ControlPlane/Testing/TestAgentHostSetup.cs` |

### Executor / false-blocker hardening

DietCode runs can lose SSE before Hermes emits `run.completed`. JoyZoning avoids blocking the lease in that case:

- **Terminal events:** only `run.completed` / `run.failed` / `run.cancelled` / `run.stopping` end a DietCode run (`message.complete` does not).
- **Stream death:** poll Hermes run status (`executor.streamStatusPollAttempts`, `streamStatusPollIntervalSeconds`); resume SSE when status is still active; cap re-attaches with `maxStreamResumeAttempts`. Resume is scheduled **after** the prior consumer releases its cancellation token (no immediate self-cancel race).
- **Reconciliation:** orphaned `Running` lease + stale execution row → Hermes poll before blocking; active → resume tracking; completed → `Verifying`.
- **Dispatch retry:** `POST /dispatch` on an existing **Leased** card skips `BeginLease` and only records dispatch attempt + starts Hermes.
- **Tool approvals:** `executor.autoApproveToolRequests` (default true) auto-approves DietCode tool prompts.

Config section: `executor:` in control-plane `appsettings` / env overrides.

## Domain rules (do not bypass in UI/CLI)

- `KanbanExecutionRules` — agent-allowed lease targets, critical dispatch, merge guards
- `LeaseOrchestrationException` → `LeaseApiResults.FromException` HTTP mapping
- `JoyZoningRuntimeContext` — worktree `.joyzoning/context.json` for `jz agent`
- Partial unique index: one **active** lease per `WorkTaskId` (migration `ExecutionLeases`)

## Desktop app (edit map)

| Area | Path |
|------|------|
| Shell / navigation | `App/ViewModels/MainWindowViewModel.cs` |
| Control plane HTTP | `App/Services/ControlPlaneClient.cs` |
| Onboarding | `App/ViewModels/OnboardingHubViewModel.cs`, `OnboardingCoordinator` |
| Kanban + leases | `App/ViewModels/KanbanViewModel.cs` |
| Hermes TUI | `App/ViewModels/ExecutionViewportViewModel.cs` + `SvcSystems.UI.Terminal` |
| Views | `App/Views/*.axaml` |

Target framework: **net8.0**, Avalonia **12**, terminal widget: **SvcSystems.UI.Terminal**.

## Adding API endpoints

1. Add route in `ApiEndpoints.MapJoyZoningApi`
2. Add matching entry in `JoyZoning.Domain/Orchestration/JoyZoningEndpointRegistry.cs` (set `AgentSafe` correctly)
3. Implement in `OrchestrationService` or `KanbanExecutionOrchestrator`
4. Map exceptions via `LeaseApiResults` for lease operations
5. Add integration test in `JoyZoning.Tests`
6. Document in [control-plane-api.md](control-plane-api.md) and/or [execution-orchestration-api.md](execution-orchestration-api.md)
7. Optional: expose via `JoyZoningCliClient` + `jz raw` or first-class `jz` subcommand

Registry sync is enforced by `JoyZoningEndpointRegistrySyncTests` and `JoyZoningLiveEndpointRegistrySyncTests`. `joyzoning doctor --json` includes an `endpoint_registry_sync` check.

Full agent-ops documentation: [agent-operations.md](agent-operations.md).

## Migrations

EF migrations live in `JoyZoning.Persistence/Migrations/`. Database is created on startup via `EnsureDatabaseCreated()`.

Current tables include: operator sessions, work tasks, execution sessions/steps, approvals, approval grants, joy_events, app_config, **execution_leases**.
