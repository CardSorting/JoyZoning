# Setup checklist

Use this page alongside the in-app **Getting Started** hub. Check items off as you go; each step links to deeper docs.

**Health grade** in the app summarizes this list: **Healthy** = all five core steps done.

---

## Phase: CONFIGURE

### ☐ 1. Control plane online

| | |
|---|---|
| **What** | JoyZoning local API on port **9470** |
| **Why** | Stores tasks, leases, timeline — desktop and `jz` both use it |
| **Verify** | `curl -s http://127.0.0.1:9470/api/health` → `"status":"ok"` |
| **Fix** | Restart app or `dotnet run --project src/JoyZoning.ControlPlane` |

### ☐ 2. diet-hermes ready

| | |
|---|---|
| **What** | One Hermes checkout with working `hermes` CLI |
| **Why** | Manager + executor are **sessions** on this install, not a second app |
| **Default path** | `~/Downloads/diet-hermes-main-master` (macOS) |
| **Verify** | `<InstallRoot>/.venv/bin/hermes --version` |
| **Fix** | `./scripts/install-diet-hermes.sh` · [hermes-setup.md](hermes-setup.md) |

---

## Phase: CONNECT

### ☐ 3. API gateway

| | |
|---|---|
| **What** | Hermes HTTP API on port **8642** (profile `joyzoning`) |
| **Why** | Manager Chat and task dispatch call this API |
| **Verify** | Green **API** chip · `curl -s http://127.0.0.1:8642/health` |
| **Fix** | **Hermes → Ensure Gateway** · `POST /api/hermes/ensure` |

### ☐ 4. Dashboard & session token

| | |
|---|---|
| **What** | Dashboard on **9119** + token stored in JoyZoning |
| **Why** | Kanban two-way sync + embedded Hermes TUI |
| **Verify** | Green **Dashboard** chip · kanban import returns tasks |
| **Fix** | **Connect dashboard** · [status-indicators.md](status-indicators.md) |

---

## Phase: OPERATE

### ☐ 5. Open a workspace

| | |
|---|---|
| **What** | A real project folder bound to an operator session |
| **Why** | Worktrees and diffs live under this repo |
| **Verify** | Title bar / Manager Chat shows your path (not only sample) |
| **Fix** | **Project → Open Workspace** |

---

## Optional milestones (recommended)

These do **not** block the health grade but confirm end-to-end flow:

| ☐ | Milestone | Confirms |
|---|-----------|----------|
| ☐ | **First Manager Chat message** | LLM API keys + model work — [api-keys-and-models.md](api-keys-and-models.md) |
| ☐ | **First kanban dispatch** | Lease + executor pipeline |
| ☐ | **Hermes TUI connected** | Dashboard WebSocket on Execution surface |

---

## Phase: READY

When all five core items are checked:

- Health grade → **Healthy**  
- **What’s next** cards appear on Getting Started  
- Safe to run production-ish tasks on low/medium risk  

Continue: [whats-next.md](whats-next.md)

---

## Printable summary

```
CONFIGURE   [ ] Control plane (:9470)
            [ ] diet-hermes (.venv/bin/hermes)

CONNECT     [ ] API gateway (:8642)
            [ ] Dashboard + token (:9119)

OPERATE     [ ] Workspace opened

OPTIONAL    [ ] Manager message  [ ] Dispatch  [ ] TUI
```

---

## Reset checklist without losing tasks

**Settings → Reset onboarding** — clears `onboarding.json` only, not `joyzoning.db`.

[← Onboarding hub](README.md)
