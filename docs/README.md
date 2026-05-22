# JoyZoning documentation

Local-first operator cockpit for supervising **Manager** and **executor** agents on a single [diet-hermes](https://github.com/NousResearch/hermes-agent) install. Kanban and execution leases coordinate work between roles — not separate Hermes checkouts.

## Start here

| Doc | Audience | Contents |
|-----|----------|----------|
| [getting-started.md](getting-started.md) | New operators | Prerequisites, first launch, Hermes setup, daily workflow |
| [architecture.md](architecture.md) | Contributors | Layers, ports, lease model, background services |
| [desktop-ui.md](desktop-ui.md) | Desktop users | Six surfaces, menus, onboarding hub |
| [cli.md](cli.md) | Terminal / CI | `jz` operator CLI and `jz agent` harness |
| [control-plane-api.md](control-plane-api.md) | Integrators | Full REST API on `:9470` |
| [execution-orchestration-api.md](execution-orchestration-api.md) | Integrators | Lease lifecycle, verification, merge, recovery |
| [configuration.md](configuration.md) | Operators / ops | `appsettings`, env vars, SQLite paths, `LeaseRuntime` |
| [event-catalog.md](event-catalog.md) | Integrators | `joy_events` types, replay, SignalR |
| [development.md](development.md) | Contributors | Build, test, scripts, project map |
| [mvp-roadmap.md](mvp-roadmap.md) | Product | Phase history (0–27) and completion checklist |
| [dogfood-report.md](dogfood-report.md) | QA | Phase 27 validation harness and fixes |

## Runtime map

```
JoyZoning.App (Avalonia)     →  http://127.0.0.1:9470  →  JoyZoning.ControlPlane
                                                      ↓
                                              http://127.0.0.1:8642  (Hermes API)
                                              http://127.0.0.1:9119  (Hermes dashboard)
```

| Component | Default URL / path |
|-----------|-------------------|
| Control plane | `http://127.0.0.1:9470` |
| Hermes API | `http://127.0.0.1:8642` |
| Hermes dashboard | `http://127.0.0.1:9119` |
| SQLite DB (macOS) | `~/Library/Application Support/JoyZoning/joyzoning.db` |
| diet-hermes install | `~/Downloads/diet-hermes-main-master` (auto-detected) |
| Onboarding prefs | `~/Library/Application Support/JoyZoning/onboarding.json` |

## Authority model (important)

JoyZoning enforces a **human / agent split** everywhere (desktop, API, `jz`, `jz agent`):

| Action | Who |
|--------|-----|
| Dispatch, critical approval, merge to Complete, revoke lease | Human operator |
| Heartbeat, verify, blocked, `ready_for_review` | Agent (`jz agent` or DietCode) |
| Direct `WorkTaskStatus.Complete` | **Forbidden** for agents — merge API only |

See [execution-orchestration-api.md](execution-orchestration-api.md) and [cli.md](cli.md).

## Example scripts

| Script | Purpose |
|--------|---------|
| `scripts/run-dev.sh` | Control plane + desktop |
| `scripts/install-jz.sh` | Install `jz` to `~/.local/bin` |
| `scripts/dogfood-validate.sh` | Run Phase 27 dogfood tests |
| `scripts/examples/*.sh` | Happy path, critical dispatch, verify failure, human merge |
