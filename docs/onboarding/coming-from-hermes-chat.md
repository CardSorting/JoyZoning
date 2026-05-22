# Coming from Hermes chat or ChatGPT

**Reading level:** Beginner — you already use AI for code; JoyZoning adds **supervision**.

---

## What changes?

| Before (chat only) | With JoyZoning |
|--------------------|----------------|
| One conversation thread | **Manager** chat + separate **worker** runs |
| “It’s done” when model says so | **Done** when tests pass **and you merge** |
| Edits in your repo directly | Edits in **sandbox worktree** first |
| You read scrollback | **Kanban + Timeline + diffs** |
| Risky tools in chat | **Approvals** inbox |

You still use **the same Hermes engine** (diet-hermes). JoyZoning is the **cockpit** around it.

---

## Mental model upgrade (2 minutes)

```
Old:  You ↔ Hermes chat ↔ your files

New:  You ↔ JoyZoning ↔ Hermes
              ├─ Manager (plan)
              ├─ Kanban (track)
              └─ Lease worktree (worker edits here)
```

---

## Migration steps (no data loss)

| Step | Action |
|------|--------|
| 1 | Keep your existing `~/.hermes` — JoyZoning adds profile **`joyzoning`** |
| 2 | Copy API keys from `~/.hermes/.env` → `~/.hermes/profiles/joyzoning/.env` |
| 3 | Point JoyZoning at your diet-hermes folder — [hermes-setup.md](hermes-setup.md) |
| 4 | **Project → Open Workspace** on your real repo |
| 5 | Optional: **Import from Hermes** on Kanban to pull existing board |

Your old Hermes sessions stay in the default profile; JoyZoning does not delete them.

---

## Habit changes (worth it)

| Old habit | New habit |
|-----------|-----------|
| “Fix it in chat” | Create a **card** → **Dispatch** |
| Trust final message | Open **Workspace** diff |
| Rerun same prompt | **Verify** with `dotnet test` (or your commands) |
| One long thread | Short Manager planning → bounded worker tasks |

---

## What stays the same

- Provider keys and models (Hermes config)  
- Skills and tools (inside Hermes)  
- You still use Cursor/VS Code to edit if you want  
- Telegram/Discord gateway (optional) — JoyZoning desktop is separate  

---

## When chat-only is still fine

| Situation | Use |
|-----------|-----|
| One-off question | Hermes CLI/TUI without JoyZoning |
| No repo / no merge gate | Plain chat |
| Production sign-off on agent work | **JoyZoning** |

---

## Next

- [before-you-begin.md](before-you-begin.md)  
- [quickstart.md](quickstart.md)  
- [concepts.md](../concepts.md)  

[← Onboarding hub](README.md)
