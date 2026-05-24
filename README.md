# JoyZoning

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](global.json)

## Supervise AI coding on your machine.

JoyZoning is a local cockpit for agent work:

- Plan in chat
- Track work as kanban cards
- Run changes in your real repo
- Verify with tests and builds
- Merge only when you approve

Chat plans. Workspace is truth.

Most coding agents stop at chat. JoyZoning adds task tracking, real repo execution, verification workflows, audit trails, and merge only when you approve — so work stays reviewable before it becomes “done.”

JoyZoning is not:

- a second IDE
- an autonomous auto-merge bot
- “the agent said it’s done”

For operators who want evidence before completion.

→ **New here?** [What's next](docs/onboarding/whats-next.md) (~20 min after install)  
→ **Full onboarding:** [onboarding/README.md](docs/onboarding/README.md)

---

## Onboarding in four steps

| Step | Goal | Time | Guide |
|------|------|------|--------|
| **0 — Decide** | Is this the workflow you want? | ~5 min | [Before you begin](docs/onboarding/before-you-begin.md) |
| **1 — Install** | App running, repo opened | ~15 min | [Quickstart](docs/onboarding/quickstart.md) |
| **2 — First merge** | Dispatch → verify → merge → Complete | ~20 min | **[What's next](docs/onboarding/whats-next.md)** |
| **3 — Daily use** | Desktop, CLI, or both | ongoing | [Setup checklist](docs/onboarding/setup-checklist.md) |

---

## Step 0 — Fit check

| You want… | Good fit? |
|-----------|-----------|
| Supervised agent work on a **real repo** with an audit trail | Yes |
| Review diffs and tests before anything ships | Yes |
| A replacement IDE or chat-only workflow | No — keep your editor; use JoyZoning to supervise |

[What is JoyZoning?](docs/what-is-joyzoning.md) (plain English)

---

## Step 1 — Install

**Need:** [.NET 8](https://dotnet.microsoft.com/download), Node 18+, pnpm, Python 3.11 (first Hermes setup), one [diet-hermes](https://github.com/NousResearch/hermes-agent) install ([guide](docs/onboarding/hermes-setup.md)), LLM API key for chat ([keys](docs/onboarding/api-keys-and-models.md)).

```bash
git clone https://github.com/CardSorting/JoyZoning.git
cd JoyZoning
pnpm install && pnpm setup && pnpm dev
```

Or: `./scripts/run-dev.sh` — [quickstart](docs/onboarding/quickstart.md).

**Ready for Step 2 when:** app open, health not **Blocked**, your repo selected (**Project → Open Workspace**). First Hermes setup may take a few minutes.

---

## Step 2 — First task (dispatch → merge)

Full walkthrough: **[What's next](docs/onboarding/whats-next.md)**.

| Step | Where | What you do | Done when… |
|------|--------|-------------|------------|
| **Plan** | Manager Chat | Describe the work | You have a reply |
| **Track** | Kanban | Create or pick a card | Card is on the board |
| **Dispatch** | Kanban | Start the agent on that card | Task activity shows a run |
| **Review files** | Workspace | Check changed files | You see the real diff |
| **Verify** | Workspace or `jz` | Run tests/build you care about | Status is ready for your review |
| **Merge** | Workspace → Kanban | Approve the change | Card is **Complete** |

Trust **Workspace**, not chat, for sign-off.

**Menus, not terminal:** [desktop menu guide](docs/onboarding/desktop-menu-guide.md) · **Glossary:** [plain-language](docs/onboarding/plain-language-glossary.md)

```bash
jz task run <task-id> --poll 10
jz task verify <task-id> --cmd "dotnet test"
jz task complete <task-id> --yes
```

---

## Step 3 — Daily use

| How you work | Start here |
|--------------|------------|
| Desktop (board + diffs) | [quickstart](docs/onboarding/quickstart.md) |
| Terminal (`jz`) | [first-run-cli](docs/onboarding/first-run-cli.md) |
| Both | [choose-your-path](docs/onboarding/choose-your-path.md) |
| Browser UI (`http://127.0.0.1:9470`) | After `pnpm dev` |

**Habits:** one active task run per card · verify before merge · trust Workspace for review · start with low risk while learning.

[Use cases](docs/use-cases.md) · [FAQ](docs/faq.md)

---

## Stuck?

| Problem | Fix |
|---------|-----|
| App won't open | [Setup troubleshooting](docs/onboarding/troubleshooting-setup.md) |
| Red API / Dashboard | [Status indicators](docs/onboarding/status-indicators.md) |
| Chat won't reply | [API keys](docs/onboarding/api-keys-and-models.md) |
| Empty Workspace | Dispatch the card first, then re-select it |
| `jz` errors | [first-run-cli](docs/onboarding/first-run-cli.md) |

---

## Advanced — multi-role delivery (optional)

After the basic loop feels natural, you can run **sequential roles** on one repo (e.g. product → architecture → implementation → QA) with a merge between each role. Same repo throughout; one role at a time.

```bash
./scripts/role-chain-dispatch.sh --create --workspace /path/to/repo --program "My App"
./scripts/role-chain-dispatch.sh --next
jz task complete <task-id> --yes
```

Details: [jsdp.md](docs/jsdp.md) · Technical background: [philosophy.md](docs/philosophy.md)

---

## Docs

| Topic | Link |
|-------|------|
| All onboarding | [onboarding/README.md](docs/onboarding/README.md) |
| Install (deep) | [installation](docs/onboarding/installation.md) |
| API | [control-plane-api](docs/control-plane-api.md) |
| Contribute | [development](docs/development.md) · [CONTRIBUTING](CONTRIBUTING.md) |

---

## For contributors

`dotnet build JoyZoning.sln` · `dotnet test tests/JoyZoning.Tests/JoyZoning.Tests.csproj` · [architecture](docs/architecture.md)

## License

[MIT](LICENSE) © 2026 CardSorting
