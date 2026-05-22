# JoyZoning documentation

**The operator cockpit for multi-agent development** — supervise Hermes Manager and executor agents on one local install; govern work with **execution leases** and **human merge**.

**License:** [MIT](../LICENSE) · **Source:** https://github.com/CardSorting/JoyZoning

---

## Start with the concept

If you read one page first, make it **[concepts.md](concepts.md)**. Everything else assumes you understand:

- **Operator cockpit** vs IDE vs raw Hermes chat  
- **One Hermes, two roles** (sessions + kanban, not two installs)  
- **Execution lease** = bounded authority + worktree + evidence  
- **Human merge** = the only path to Complete  

---

## Learning paths

### Understand the product (recommended order)

1. [concepts.md](concepts.md) — pillars, authority split, sequence diagram  
2. [use-cases.md](use-cases.md) — nine real scenarios  
3. [getting-started.md](getting-started.md) — clone, configure, first launch  
4. [faq.md](faq.md) — quick answers  

### Run it day to day

1. [getting-started.md](getting-started.md)  
2. [desktop-ui.md](desktop-ui.md) or [cli.md](cli.md)  
3. [hermes-integration.md](hermes-integration.md)  
4. [troubleshooting.md](troubleshooting.md)  

### Govern work (leases & merge)

1. [lease-lifecycle.md](lease-lifecycle.md)  
2. [execution-orchestration-api.md](execution-orchestration-api.md)  
3. [glossary.md](glossary.md)  

### Integrate or extend

1. [architecture.md](architecture.md)  
2. [control-plane-api.md](control-plane-api.md)  
3. [event-catalog.md](event-catalog.md)  
4. [development.md](development.md)  
5. [../CONTRIBUTING.md](../CONTRIBUTING.md)  

---

## Full index

| Doc | Audience | Contents |
|-----|----------|----------|
| **[concepts.md](concepts.md)** | **Everyone** | **Product spine — read first** |
| [use-cases.md](use-cases.md) | Operators | Scenario walkthroughs |
| [faq.md](faq.md) | Everyone | Short Q&A |
| [getting-started.md](getting-started.md) | New operators | Install, first launch |
| [hermes-integration.md](hermes-integration.md) | Operators / integrators | Gateway, dashboard, kanban, SSE |
| [lease-lifecycle.md](lease-lifecycle.md) | Everyone | State machine, evidence |
| [architecture.md](architecture.md) | Contributors | Layers, services, schema |
| [desktop-ui.md](desktop-ui.md) | Desktop users | Surfaces, menus, onboarding |
| [cli.md](cli.md) | Terminal / CI | `jz` and `jz agent` |
| [control-plane-api.md](control-plane-api.md) | Integrators | REST on `:9470` |
| [execution-orchestration-api.md](execution-orchestration-api.md) | Integrators | Dispatch, verify, merge |
| [configuration.md](configuration.md) | Operators / ops | appsettings, env, `LeaseRuntime` |
| [troubleshooting.md](troubleshooting.md) | Operators | Symptom → fix |
| [glossary.md](glossary.md) | Everyone | Vocabulary |
| [event-catalog.md](event-catalog.md) | Integrators | Events, SignalR |
| [development.md](development.md) | Contributors | Build, test |
| [mvp-roadmap.md](mvp-roadmap.md) | Product | Phase 0–27 |
| [dogfood-report.md](dogfood-report.md) | QA | Validation harness |

---

## Runtime map

```
JoyZoning.App          →  http://127.0.0.1:9470  →  JoyZoning.ControlPlane
                                                    ↓
                                            http://127.0.0.1:8642  (Hermes API)
                                            http://127.0.0.1:9119  (Hermes dashboard)
```

| Component | Default |
|-----------|---------|
| Control plane | `http://127.0.0.1:9470` |
| Hermes API | `http://127.0.0.1:8642` |
| Hermes dashboard | `http://127.0.0.1:9119` |
| SQLite (macOS) | `~/Library/Application Support/JoyZoning/joyzoning.db` |
| Example config | `src/JoyZoning.ControlPlane/appsettings.example.json` |

---

## Authority at a glance

| Action | Operator (you) | Agent |
|--------|----------------|-------|
| Dispatch, critical approval | ✓ | — |
| Code in lease worktree | — | ✓ |
| Verify + evidence | ✓ (via `jz task`) | ✓ (via `jz agent`) |
| Merge → Complete | ✓ | **never** |
| Revoke lease | ✓ | — |

Details: [concepts.md#human-vs-agent-authority](concepts.md#human-vs-agent-authority).

---

## Scripts

| Script | Purpose |
|--------|---------|
| `scripts/run-dev.sh` | Control plane + desktop |
| `scripts/install-jz.sh` | `~/.local/bin/jz` |
| `scripts/install-diet-hermes.sh` | Hermes stack |
| `scripts/dogfood-validate.sh` | E2E governance tests |
| `scripts/examples/*.sh` | Happy path, critical, verify fail, merge |
