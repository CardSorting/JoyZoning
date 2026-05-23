# Development guide

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

`global.json` pins **8.0.421**. On macOS, `/usr/local/share/dotnet` is often **.NET 6 only** while **.NET 8** is installed under `~/.dotnet`.

```bash
source scripts/dotnet-env.sh   # sets DOTNET_ROOT + PATH
dotnet --list-sdks             # should show 8.0.421
```

If no 8.x SDK is listed:

```bash
curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0
source scripts/dotnet-env.sh
```

Add to your shell profile (optional):

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
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

## Live workspace mirror (DietCode progress)

JoyZoning builds in `.joyzoning/worktrees/<task-id>/`, not the session folder root. To see progress in your IDE without manual `rsync`:

| Mechanism | What you get |
|-----------|----------------|
| **`JOYZONING_LIVE.md`** | Auto-written at the session workspace root every ~10s while a lease is active (`Workspace:MirrorToSessionRoot`) |
| **`GET /api/tasks/{id}/live`** | JSON snapshot + triggers mirror; includes `recommendedPollSeconds` |
| **`POST /api/tasks/{id}/live/refresh`** | Force mirror + status file update |
| **`jz task watch <id> [--workspace path]`** | Same as workspace-live.sh — step tracker + adaptive poll |
| **`./scripts/workspace-live.sh <task-id> [workspace]`** | Friendly terminal tracker (steps, % bar, plain language; 3–25s adaptive poll) |
| **`.joyzoning/live.json`** | Machine-readable mirror of `display` for IDE tooling |

Config (`appsettings` / `Workspace` section):

```json
"Workspace": {
  "MirrorToSessionRoot": true,
  "LiveStatusFileName": "JOYZONING_LIVE.md",
  "LiveMonitorIntervalSeconds": 10
}
```

Open `TinyQuest-Campfire/JOYZONING_LIVE.md` (or your session root) while a task runs.

**Terminal watcher** (dedicated pane while DietCode runs):

```bash
./scripts/workspace-live.sh f6d456dc-707b-427c-b130-6459529db5cc /path/to/session-workspace
# once:  ./scripts/workspace-live.sh --once <task-id>
# fixed: JOYZONING_LIVE_INTERVAL=10 ./scripts/workspace-live.sh <task-id>
```

The tracker uses familiar patterns (install wizard / package tracker):

- **Headline + subheadline** in plain language (not lease enum names)
- **Step list** — Getting started → Building → Quality checks → Review
- **Progress bar** — combined build stage + deliverables checklist
- **“What you can do next”** when blocked or ready for review
- **`JOYZONING_LIVE.md`** — same narrative for non-technical readers (IDE-friendly)

API clients can read `display` on `GET /api/tasks/{id}/live` (`headline`, `steps`, `nextActions`, `progressPercent`, `activityState`).

Options: `--simple`, `--paths`, `--once`, `--notify` (macOS), `JOYZONING_LIVE_OPEN=1`.

Polling modes (`display.pollMode`): **burst** (~3s) while files are changing, **normal**, **slow** (~20s) when idle/stuck, **stopped** when done.

On TTY, compact **in-place** status lines appear between full dashboard redraws (like `npm` / CI log tail). Full refresh shows **Step N of M**, timeline `●──◉──○──○`, ETA estimate, and **Good to know** tips.

## Run (development)

| Command | What it does |
|---------|----------------|
| `./scripts/run-dev.sh` | Control plane (background) + desktop |
| `dotnet run --project src/JoyZoning.ControlPlane` | API only on `:9470` |
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
| `JoyZoning.Cli.Tests` | Args, safety, JSON output, agent guard |

Dogfood uses `JoyZoningDogfoodServerProcess` (real Kestrel port) + shared temp SQLite — see [dogfood-report.md](dogfood-report.md).

**Orchestration API integration tests** (`OrchestrationApi` xUnit collection):

- Shared `WebApplicationFactory` + **in-memory SQLite** (`Mode=Memory;Cache=Shared`) — avoids disk I/O errors from `EnsureDeletedAsync` while the host holds connections open.
- `TestDatabaseReset.ClearAllAsync` deletes rows instead of dropping the database file between tests.
- `HermesRunEventConsumer.TrackRun` is a **no-op** in `Testing` so stub SSE reconciliation does not race lease assertions across test methods.
- Filtered run: `dotnet test tests/JoyZoning.Tests/JoyZoning.Tests.csproj --filter "FullyQualifiedName~OrchestrationApi"`.

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
2. Implement in `OrchestrationService` or `KanbanExecutionOrchestrator`
3. Map exceptions via `LeaseApiResults` for lease operations
4. Add integration test in `JoyZoning.Tests`
5. Document in [control-plane-api.md](control-plane-api.md) and/or [execution-orchestration-api.md](execution-orchestration-api.md)
6. Optional: expose via `JoyZoningCliClient` + `jz raw` or first-class `jz` subcommand

## Migrations

EF migrations live in `JoyZoning.Persistence/Migrations/`. Database is created on startup via `EnsureDatabaseCreated()`.

Current tables include: operator sessions, work tasks, execution sessions/steps, approvals, approval grants, joy_events, app_config, **execution_leases**.
