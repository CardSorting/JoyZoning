# JoyZoning documentation

**The operator cockpit for multi-agent development** — supervise Hermes Manager and executor agents on one local install; govern work with **execution leases** and **human merge**.

**License:** [MIT](../LICENSE) · **Source:** https://github.com/CardSorting/JoyZoning

---

## New here?

```mermaid
flowchart TD
  Start([New operator]) --> Q{Prefer GUI or terminal?}
  Q -->|GUI| QS[onboarding/quickstart.md]
  Q -->|Terminal| CLI[onboarding/first-run-cli.md]
  Q -->|Learn first| C[concepts.md]
  QS --> CHK[onboarding/setup-checklist.md]
  CLI --> CHK
  CHK --> NEXT[onboarding/whats-next.md]
```

| Step | Page | Time |
|------|------|------|
| 1 | [Onboarding hub](onboarding/README.md) | — |
| 2 | [5-minute quickstart](onboarding/quickstart.md) or [Choose your path](onboarding/choose-your-path.md) | 5–20 min |
| 3 | [Setup checklist](onboarding/setup-checklist.md) | 10 min |
| 4 | [What's next](onboarding/whats-next.md) | 20 min |

**Concepts (why JoyZoning exists):** [concepts.md](concepts.md) · **FAQ:** [faq.md](faq.md)

---

## Documentation by role

### I am a new operator (non-technical friendly)

| Doc | Contents |
|-----|----------|
| **[onboarding/README.md](onboarding/README.md)** | **Start here** — “I'm stuck”, reading levels, full index |
| [before-you-begin.md](onboarding/before-you-begin.md) | Do you need JoyZoning? Time, keys, privacy |
| [desktop-menu-guide.md](onboarding/desktop-menu-guide.md) | Where to click (no Terminal) |
| [plain-language-glossary.md](onboarding/plain-language-glossary.md) | Words explained with analogies |
| [troubleshooting-setup.md](onboarding/troubleshooting-setup.md) | Setup decision trees |
| [quickstart.md](onboarding/quickstart.md) | Desktop in ~5–15 min |
| [whats-next.md](onboarding/whats-next.md) | First dispatch → merge |

### I run JoyZoning day to day

| Doc | Contents |
|-----|----------|
| [getting-started.md](getting-started.md) | Overview + links |
| [desktop-ui.md](desktop-ui.md) | All six surfaces |
| [cli.md](cli.md) | `jz` / `jz agent` |
| [hermes-aligned-terminal-strategy.md](hermes-aligned-terminal-strategy.md) | Cognition vs authority; `jz` vs `hermes --tui` |
| [use-cases.md](use-cases.md) | Scenario walkthroughs |
| [troubleshooting.md](troubleshooting.md) | Symptom → fix |

### I install or administer locally

| Doc | Contents |
|-----|----------|
| [installation.md](onboarding/installation.md) | Full install + .NET 8 |
| [hermes-setup.md](onboarding/hermes-setup.md) | diet-hermes profile `joyzoning` |
| [configuration.md](configuration.md) | appsettings, env, leases |
| [hermes-integration.md](hermes-integration.md) | Ports, token, kanban sync |

### I need governance / audit detail

| Doc | Contents |
|-----|----------|
| [lease-lifecycle.md](lease-lifecycle.md) | State machine, evidence |
| [execution-orchestration-api.md](execution-orchestration-api.md) | Dispatch, verify, merge |
| [glossary.md](glossary.md) | Vocabulary |

### I integrate or contribute

| Doc | Contents |
|-----|----------|
| [architecture.md](architecture.md) | Layers, services |
| [control-plane-api.md](control-plane-api.md) | REST `:9470` |
| [event-catalog.md](event-catalog.md) | SignalR events |
| [development.md](development.md) | Build, test |
| [../CONTRIBUTING.md](../CONTRIBUTING.md) | PRs |

---

## Onboarding section (17 guides)

**Hub:** [onboarding/README.md](onboarding/README.md) — **Stuck?** [troubleshooting-setup.md](onboarding/troubleshooting-setup.md)

| Beginner | Operator | Technical |
|----------|----------|-----------|
| [before-you-begin](onboarding/before-you-begin.md) | [quickstart](onboarding/quickstart.md) | [installation](onboarding/installation.md) |
| [plain-language-glossary](onboarding/plain-language-glossary.md) | [whats-next](onboarding/whats-next.md) | [first-run-cli](onboarding/first-run-cli.md) |
| [desktop-menu-guide](onboarding/desktop-menu-guide.md) | [setup-checklist](onboarding/setup-checklist.md) | [platform-linux](onboarding/platform-linux.md) |
| [coming-from-hermes-chat](onboarding/coming-from-hermes-chat.md) | [status-indicators](onboarding/status-indicators.md) | [hermes-setup](onboarding/hermes-setup.md) |
| [platform-macos](onboarding/platform-macos.md) | [choose-your-path](onboarding/choose-your-path.md) | [settings-explained](onboarding/settings-explained.md) |

Also: [first-run-desktop](onboarding/first-run-desktop.md) · [api-keys-and-models](onboarding/api-keys-and-models.md) · [troubleshooting-setup](onboarding/troubleshooting-setup.md)

---

## Core concepts (one paragraph)

JoyZoning is **not** an IDE and **not** a second Hermes. It is a **local cockpit** where you plan in Manager Chat, track work on kanban, dispatch executors into **isolated worktrees**, and **merge only after verification**. Agents cannot mark tasks Complete without you. Details: [concepts.md](concepts.md).

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

---

## All docs (alphabetical)

| Doc | Audience |
|-----|----------|
| [architecture.md](architecture.md) | Contributors |
| [cli.md](cli.md) | Terminal / CI |
| [concepts.md](concepts.md) | Everyone |
| [configuration.md](configuration.md) | Operators / ops |
| [control-plane-api.md](control-plane-api.md) | Integrators |
| [desktop-ui.md](desktop-ui.md) | Desktop users |
| [dogfood-report.md](dogfood-report.md) | QA |
| [event-catalog.md](event-catalog.md) | Integrators |
| [execution-orchestration-api.md](execution-orchestration-api.md) | Integrators |
| [faq.md](faq.md) | Everyone |
| [getting-started.md](getting-started.md) | New operators |
| [hermes-integration.md](hermes-integration.md) | Operators / integrators |
| [hermes-aligned-terminal-strategy.md](hermes-aligned-terminal-strategy.md) | Terminal / CLI architecture |
| [lease-lifecycle.md](lease-lifecycle.md) | Everyone |
| [mvp-roadmap.md](mvp-roadmap.md) | Product |
| [troubleshooting.md](troubleshooting.md) | Operators |
| [use-cases.md](use-cases.md) | Operators |
| [yolo-mode.md](yolo-mode.md) | Operators / automation |
| [glossary.md](glossary.md) | Everyone |
| [development.md](development.md) | Contributors |

---

## Scripts

| Script | Purpose |
|--------|---------|
| `scripts/run-dev.sh` | Control plane + desktop |
| `scripts/install-jz.sh` | `~/.local/bin/jz` |
| `scripts/install-diet-hermes.sh` | Hermes stack |
| `scripts/jz-env.sh` | Shell exports for CLI |
| `scripts/dogfood-validate.sh` | E2E governance tests |
| `scripts/examples/*.sh` | Happy path, critical, verify fail, merge |
