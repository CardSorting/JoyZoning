# Contributing to JoyZoning

Thank you for contributing. JoyZoning is MIT-licensed — see [LICENSE](LICENSE).

## Before you start

1. Read [docs/philosophy.md](docs/philosophy.md), then [docs/concepts.md](docs/concepts.md), [docs/architecture.md](docs/architecture.md), and [docs/development.md](docs/development.md).
2. Understand the **authority model**: agents cannot merge or mark tasks Complete; humans own dispatch and merge ([docs/execution-orchestration-api.md](docs/execution-orchestration-api.md)).
3. Install [.NET 8 SDK](https://dotnet.microsoft.com/download) (see `global.json`).

## Local setup

```bash
git clone https://github.com/CardSorting/JoyZoning.git
cd JoyZoning
cp src/JoyZoning.ControlPlane/appsettings.example.json src/JoyZoning.ControlPlane/appsettings.Development.json
# Edit Hermes:InstallRoot to your diet-hermes checkout

dotnet build JoyZoning.sln
./scripts/run-tests.sh          # fast: unit tests only
./scripts/run-tests.sh medium   # + API integration tests
```

Optional: `./scripts/run-tests.sh dogfood` for end-to-end lease + CLI paths.

## Pull requests

- Keep PRs focused; one concern per PR when possible.
- Add or update tests for behavior changes (`JoyZoning.Tests` for API/orchestration, `JoyZoning.Cli.Tests` for CLI).
- Update relevant docs under `docs/` when you change APIs, config keys, or operator workflows.
- Do not commit secrets (API keys, dashboard tokens, personal paths in `appsettings.json` — use `appsettings.Development.json` locally).

## Code conventions

- Match existing C# style in the file you edit (nullable enabled, `async`/`await` for I/O).
- Lease transitions belong in `KanbanExecutionOrchestrator` / `KanbanExecutionRules`, not duplicated in UI or CLI.
- User-facing paths: prefer Application Support / env overrides over hardcoded home directories in new code.
- Hermes integration stays in `JoyZoning.Agents`; control plane exposes HTTP only.

## Reporting issues

Include:

- OS and .NET version (`dotnet --version`)
- JoyZoning surface (desktop, `jz`, API)
- Steps to reproduce
- Output of `jz doctor` or **Settings → Copy health report** when Hermes/connectivity is involved

## Questions

Open a [GitHub discussion or issue](https://github.com/CardSorting/JoyZoning/issues) on the repository.
