# Before you begin

**Reading level:** Beginner — no terminal experience required.

Answer these questions **before** installing. Saves time if JoyZoning is not the right tool for your goal today.

---

## Is JoyZoning what you need?

| Your goal | JoyZoning helps? | Alternative |
|-----------|------------------|-------------|
| Chat with an AI about code | Partially — use **Manager Chat**, but Cursor/Copilot may be simpler | IDE copilot |
| Run one script and forget it | No — JoyZoning adds review and merge gates | Plain Hermes CLI |
| **Supervise agents** on a real repo with audit trail | **Yes** | — |
| **Approve** what ships (tests + human sign-off) | **Yes** | — |
| Team kanban synced with Hermes | **Yes** (with dashboard connected) | Hermes kanban alone |

JoyZoning is an **operator cockpit** — like a flight deck, not the airplane engine. The engine is **diet-hermes** (Hermes Agent).

---

## The one-sentence version

You **plan** with a Manager agent, **assign** work on a board, agents **work in a sandbox folder**, you **run checks**, then **you approve** before the task is truly done.

---

## What you do *not* need

| Myth | Reality |
|------|---------|
| Two Hermes installs | **One** diet-hermes; Manager and worker are different *sessions* |
| Replace VS Code / Cursor | Keep your editor; JoyZoning watches agent work |
| Cloud account with JoyZoning | Everything is **local** on your Mac/Linux |
| Trust the agent’s “I’m done” | **You** merge after verification |

---

## What you *do* need

### Hardware & OS

| | Minimum |
|---|---------|
| Computer | Mac (recommended) or Linux PC |
| RAM | 8 GB+ (16 GB comfortable for Hermes + desktop) |
| Disk | ~2 GB free for first-time AI stack install |
| Internet | First setup + when the AI model calls your provider |

### Accounts & keys

| | Required when? |
|---|----------------|
| **LLM provider key** (OpenRouter, Google, OpenAI, …) | Before Manager Chat or dispatch can run |
| JoyZoning login | **Never** — no cloud signup |
| GitHub for JoyZoning | Only to clone the repo |

Keys live in `~/.hermes/profiles/joyzoning/.env` — see [api-keys-and-models.md](api-keys-and-models.md).

### Skills (honest list)

| Skill | Desktop path | CLI path |
|-------|--------------|----------|
| Open an app and use menus | Enough | — |
| Copy-paste into Terminal | Helpful | Required |
| Know your project folder path | Helpful | Required |
| Read a kanban board | Helpful | Optional |

**Non-technical path:** [quickstart.md](quickstart.md) + [desktop-menu-guide.md](desktop-menu-guide.md) — skip Terminal until you want it.

---

## How long does setup take?

| Situation | Time |
|-----------|------|
| First time (downloads AI stack) | **15–25 minutes** |
| You already built diet-hermes | **5–10 minutes** |
| Returning user (services stopped) | **1–2 minutes** (Connect all) |

---

## Familiar products (mental model)

| You know… | JoyZoning is like… |
|-----------|-------------------|
| **Trello / Linear** | Kanban board — but cards spawn agent work in isolated folders |
| **GitHub Pull Requests** | Merge only after checks — agents cannot merge for you |
| **Docker Desktop** | Green/red status for “is the engine running?” |
| **VS Code Welcome** | Getting Started checklist with phases |
| **Slack approvals** | Approvals inbox for risky agent tools |

---

## Privacy & data (plain language)

| Data | Stays where |
|------|-------------|
| Your source code | Your disk — worktrees under your repo |
| Tasks, leases, timeline | Local SQLite on your machine |
| Chat with AI | Sent to **your** LLM provider (per Hermes config) |
| JoyZoning cloud | **None** — no telemetry server in MVP |

---

## Ready?

| Next step | Link |
|-----------|------|
| Pick desktop or terminal | [choose-your-path.md](choose-your-path.md) |
| Fastest desktop start | [quickstart.md](quickstart.md) |
| Words explained simply | [plain-language-glossary.md](plain-language-glossary.md) |
| Full install detail | [installation.md](installation.md) |

[← Onboarding hub](README.md)
