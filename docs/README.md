# JoyZoning documentation

Local-first operator cockpit for supervising **Manager** and **executor** agents on a single [diet-hermes](https://github.com/NousResearch/hermes-agent) install. Kanban and execution leases coordinate work between roles — not separate Hermes checkouts.

**License:** [MIT](../LICENSE) · **Source:** https://github.com/CardSorting/JoyZoning

---

## Learning paths

### I want to run JoyZoning today

1. [getting-started.md](getting-started.md) — clone, configure, first launch  
2. [hermes-integration.md](hermes-integration.md) — what diet-hermes must provide  
3. [troubleshooting.md](troubleshooting.md) — if something fails  

### I operate from the terminal

1. [cli.md](cli.md) — `jz` workflows and recipes  
2. [lease-lifecycle.md](lease-lifecycle.md) — when to merge vs revoke  
3. [execution-orchestration-api.md](execution-orchestration-api.md) — HTTP contract  

### I integrate or extend the control plane

1. [architecture.md](architecture.md) — layers and services  
2. [control-plane-api.md](control-plane-api.md) — REST + SignalR  
3. [event-catalog.md](event-catalog.md) — audit stream  
4. [development.md](development.md) — build, test, edit map  
5. [../CONTRIBUTING.md](../CONTRIBUTING.md) — PR expectations  

### I need a definition

- [glossary.md](glossary.md) — terms and enum shorthand  

---

## Full index

| Doc | Audience | Contents |
|-----|----------|----------|
| [getting-started.md](getting-started.md) | New operators | Prerequisites, first launch, daily workflow |
| [hermes-integration.md](hermes-integration.md) | Operators / integrators | Gateway, dashboard, kanban sync, SSE |
| [lease-lifecycle.md](lease-lifecycle.md) | Everyone | Lease state machine, evidence, failure paths |
| [architecture.md](architecture.md) | Contributors | Layers, ports, persistence, background jobs |
| [desktop-ui.md](desktop-ui.md) | Desktop users | Surfaces, menus, onboarding hub |
| [cli.md](cli.md) | Terminal / CI | `jz` and `jz agent` |
| [control-plane-api.md](control-plane-api.md) | Integrators | REST on `:9470` |
| [execution-orchestration-api.md](execution-orchestration-api.md) | Integrators | Dispatch, verify, merge, recovery |
| [configuration.md](configuration.md) | Operators / ops | appsettings, env, `LeaseRuntime` |
| [troubleshooting.md](troubleshooting.md) | Operators | Symptom → fix tables |
| [glossary.md](glossary.md) | Everyone | Vocabulary |
| [event-catalog.md](event-catalog.md) | Integrators | `joy_events`, replay, SignalR |
| [development.md](development.md) | Contributors | Build, test, scripts |
| [mvp-roadmap.md](mvp-roadmap.md) | Product | Phase 0–27 checklist |
| [dogfood-report.md](dogfood-report.md) | QA | Phase 27 validation report |

---

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
| diet-hermes install | Set via `Hermes:InstallRoot` (see [configuration.md](configuration.md)) |
| Onboarding prefs | `~/Library/Application Support/JoyZoning/onboarding.json` |
| Example config | `src/JoyZoning.ControlPlane/appsettings.example.json` |

---

## Authority model (important)

JoyZoning enforces a **human / agent split** everywhere (desktop, API, `jz`, `jz agent`):

| Action | Who |
|--------|-----|
| Dispatch, critical approval, merge to Complete, revoke lease | Human operator |
| Heartbeat, verify, blocked, `ready_for_review` | Agent (`jz agent` or DietCode) |
| Direct `WorkTaskStatus.Complete` | **Forbidden** for agents — merge API only |

See [lease-lifecycle.md](lease-lifecycle.md), [execution-orchestration-api.md](execution-orchestration-api.md), and [cli.md](cli.md).

---

## Example scripts

| Script | Purpose |
|--------|---------|
| `scripts/run-dev.sh` | Control plane + desktop |
| `scripts/install-jz.sh` | Install `jz` to `~/.local/bin` |
| `scripts/install-diet-hermes.sh` | One-shot Hermes stack install |
| `scripts/dogfood-validate.sh` | Phase 27 dogfood tests |
| `scripts/examples/*.sh` | Happy path, critical dispatch, verify failure, human merge |
