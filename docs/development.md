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

## Run (development)

| Command | What it does |
|---------|----------------|
| `./scripts/run-dev.sh` | Control plane (background) + desktop |
| `dotnet run --project src/JoyZoning.ControlPlane` | API only on `:9470` |
| `dotnet run --project src/JoyZoning.App` | Desktop (auto-starts control plane) |
| `./scripts/jz doctor` | CLI smoke check without installing `jz` |

## Test

```bash
source scripts/dotnet-env.sh

# CLI tests (in solution)
dotnet test tests/JoyZoning.Cli.Tests/JoyZoning.Cli.Tests.csproj

# Control plane + orchestration + dogfood
dotnet test tests/JoyZoning.Tests/JoyZoning.Tests.csproj

# Dogfood only (subprocess server + real jz binary)
./scripts/dogfood-validate.sh
```

Key test areas:

| File / area | Covers |
|-------------|--------|
| `OrchestrationApiIntegrationTests` | HTTP status codes for lease API |
| `KanbanExecutionOrchestratorTests` | Transition matrix, evidence |
| `LeaseRuntimeServiceTests` | Stale heartbeat, expiration, caps |
| `DogfoodValidationTests` | End-to-end `jz` / `jz agent` paths |
| `JoyZoning.Cli.Tests` | Args, safety, JSON output, agent guard |

Dogfood uses `JoyZoningDogfoodServerProcess` (real Kestrel port) + shared temp SQLite — see [dogfood-report.md](dogfood-report.md).

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
| Hermes SSE consumer | `ControlPlane/Services/HermesRunEventConsumer.cs` |
| Testing stubs | `ControlPlane/Testing/TestAgentHostSetup.cs` |

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
