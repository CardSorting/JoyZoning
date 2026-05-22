# Desktop menu guide

**Reading level:** Beginner — where to click, no Terminal required.

Maps JoyZoning menus to outcomes. Familiar if you have used **VS Code**, **Figma**, or **Linear**.

---

## Window layout (30 seconds)

```
┌─────────────────────────────────────────────────────────┐
│  [API ●] [Dashboard ●]     Status chips (top)           │
├──────────────┬──────────────────────────────────────────┤
│  Sidebar     │  Main surface (Manager / Kanban / …)     │
│  · Getting   │                                          │
│    Started   │                                          │
│  · Manager   │                                          │
│  · Kanban    │                                          │
│  · Execution │                                          │
│  · Workspace │                                          │
│  · Approvals │                                          │
│  · Timeline  │                                          │
└──────────────┴──────────────────────────────────────────┘
```

**Sidebar** = switch surfaces (like VS Code Activity Bar).

---

## I want to… → Go here

| Goal | Where to click |
|------|----------------|
| See if setup is done | Sidebar → **Getting Started** |
| Fix red status chips | Click chip **or** menu **Hermes → Connection…** |
| Talk to planning AI | **Manager Chat** |
| Create / move tasks | **Kanban** |
| Start agent on a card | Kanban → select card → **Dispatch** |
| Watch agent work | **Execution** |
| See file changes | **Workspace** |
| Allow/deny risky tool | **Approvals** |
| Debug what happened | **Timeline** |
| Open my real project | **Project → Open Workspace** |
| Copy support bundle | **Settings → Copy health report** |

---

## Menu bar reference

### Project

| Item | Does what |
|------|-----------|
| **Open Workspace** | Bind JoyZoning to your repo folder (required for real work) |
| **Open last** | Reopen previous workspace from memory |

### Hermes

| Item | Does what |
|------|-----------|
| **Connection…** | Install path, test connection, connect dashboard, refresh token |
| **Ensure Gateway** | Start API if **API** chip is red |
| **(status chips)** | Shortcut to Connection window |

### Settings

| Item | Does what |
|------|-----------|
| **Open Getting Started** | Checklist + health grade + smart setup |
| **Copy health report** | Paste into bug reports (no secrets) |
| **Reset onboarding** | Show checklist again; does **not** delete tasks |

### Recovery (when shown)

| Item | Does what |
|------|-----------|
| Resume / cancel interrupted run | After app restart mid-execution |

---

## Getting Started hub (checklist home)

| UI element | Action |
|------------|--------|
| **Health grade** | Healthy / Degraded / Blocked — [status-indicators.md](status-indicators.md) |
| **Numbered steps** | Tap **Fix** / **Run** on incomplete rows |
| **Quick actions** | Shortcuts: Smart setup, Wizard, Connection, Workspace, TUI |
| **Playbooks** | Expandable “if control plane offline” style fixes |
| **What’s next cards** | After core green — links to Manager, Kanban, Execution |

---

## Manager Chat

| Control | Does what |
|---------|-----------|
| Message box + Send | Planning conversation with Hermes Manager |
| **Parse** | Turn last reply bullets into a list |
| **→ Task** | Create one kanban card from last reply |

**Tip:** First message failing? [api-keys-and-models.md](api-keys-and-models.md)

---

## Kanban

| Control | Does what |
|---------|-----------|
| **New Task** | Title, description, agent, risk level |
| **Drag card** | Change column / status |
| **Dispatch** | Start executor in worktree (needs API green) |
| **Critical checkbox** | Required for risk 3 before dispatch |
| **Import from Hermes** | Pull board from Hermes (needs Dashboard green) |
| **Merge** (when offered) | Your sign-off → Complete |

Agents **cannot** drag to Complete without verification + merge.

---

## Execution

| Control | Does what |
|---------|-----------|
| Step list | Live tool events from Hermes run |
| Terminal preview | Recent command output |
| **Connect dashboard & TUI** | Full embedded Hermes terminal (PTY) |

---

## Workspace

| Panel | Does what |
|-------|-----------|
| File tree | Browse workspace root |
| Changed files | Git-detected changes in worktree/repo |
| Diff | Red/green split — review before merge |

---

## Approvals

| Button | Does what |
|--------|-----------|
| **Once** | Allow this request only |
| **Task** | Allow similar requests for this card |
| **Session** | Allow for this workspace session |
| **Deny** | Block |

---

## Settings window (gear)

Common toggles — full list: [settings-explained.md](settings-explained.md)

| Setting | Plain meaning |
|---------|---------------|
| Automatic setup on launch | Run smart setup when app opens |
| Auto-connect on startup | Try Hermes connection automatically |
| Hermes install path | Folder containing diet-hermes |
| Kanban auto-sync | Periodically import from Hermes board |

---

## Keyboard & tips

| Tip | |
|-----|---|
| Red chip | Click it — don’t dig in logs first |
| Stuck overlay | Wait on first install; then Smart setup |
| Dismissed tips | Settings → Reset onboarding to see coach marks again |

---

## Next

- [quickstart.md](quickstart.md)  
- [whats-next.md](whats-next.md)  
- [desktop-ui.md](../desktop-ui.md) — technical detail  

[← Onboarding hub](README.md)
