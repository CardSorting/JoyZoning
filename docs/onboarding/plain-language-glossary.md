# Plain-language glossary

**Reading level:** Beginner — everyday words first, technical name in parentheses.

For precise API terms see [glossary.md](../glossary.md). **How one card maps to one folder:** [workspace-state.md](../workspace-state.md).

---

## People and roles

| Everyday term | Technical term | Think of it as… |
|---------------|----------------|-----------------|
| **You** | Operator | The person who approves work — like a tech lead or release manager |
| **Manager** | Manager session / Hermes planning run | Senior colleague who breaks down goals — does not edit files directly |
| **Worker / executor** | DietCode / executor session | Contractor working in a cordoned-off copy of the repo |
| **Agent** | Hermes run with tools | The worker when it is actively running |

---

## Places (where things live)

| Everyday term | Technical term | Think of it as… |
|---------------|----------------|-----------------|
| **JoyZoning app** | `JoyZoning.App` | Mission control window |
| **Local coordinator** | Control plane (`:9470`) | Air traffic control — remembers tasks and rules |
| **AI engine** | diet-hermes | One engine; different “channels” for manager vs worker |
| **Your project folder** | Session workspace / `workspaceRoot` | The repo you opened — same idea as “Open Folder” in VS Code |
| **Sandbox copy** | Lease worktree | A practice lane — agent edits here after **Dispatch**, like a CI job workspace |
| **Files changed tab** | Workspace → Changed | Same idea as GitHub PR “Files changed” — one card, one folder |
| **1:1 workspace state** | Card-scoped inspection | Pick a card → every panel shows that card’s folder (not chat guesswork) |
| **Hidden agent notes** | `.joyzoning/context.json` | Sticky note in the sandbox: task id, what agent may/may not do |

---

## Work items

| Everyday term | Technical term | Think of it as… |
|---------------|----------------|-----------------|
| **Card** | Work task | One piece of work on the kanban board |
| **Column** | `WorkTaskStatus` | Backlog → In progress → Done (with extra safety columns) |
| **Assignment package** | Execution lease | Permission slip: where to work, how risky, when it expires |
| **Proof tests ran** | Verification report | CI green check — attached to the card before you sign off |
| **Audit trail** | Evidence log / Timeline | Security camera log — who did what, when |

---

## Actions (what buttons mean)

| Everyday term | Technical term | Who can do it |
|---------------|----------------|---------------|
| **Start worker on card** | Dispatch | **You** |
| **Approve risky action** | Approval resolve (Once/Task/Session/Deny) | **You** |
| **Run tests as proof** | Verify | **You** or worker (`jz agent verify`) |
| **Sign off / ship it** | Merge → Complete | **You only** — never the agent |
| **Cancel assignment** | Revoke lease | **You** |
| **Worker finished my part** | `ready_for_review` / `agent done` | Worker — still needs **your** merge |

---

## Status chips (top of app)

| Chip | Means |
|------|-------|
| **API** green | AI engine’s phone line is up (`:8642`) |
| **API** red | Start gateway — [status-indicators.md](status-indicators.md) |
| **Dashboard** green | Board sync + embedded terminal auth OK |
| **Dashboard** red | Connect dashboard — one-click in Connection window |

---

## Risk levels (when creating a card)

| Level | Everyday meaning |
|-------|------------------|
| **Low (1)** | Normal task — default for learning |
| **Medium (2)** | Bigger change — still routine |
| **High / Critical (3)** | Sensitive (prod, secrets) — you must tick approval each dispatch; only one such job at a time globally |

---

## Ports (only if someone asks)

| Number | Service in plain words |
|--------|------------------------|
| **9470** | JoyZoning coordinator |
| **8642** | Hermes API (chat + dispatch) |
| **9119** | Hermes dashboard (board + terminal embed) |

All are **localhost** — not exposed to the internet by default.

---

## Phrases you might hear

| Phrase | Meaning |
|--------|---------|
| “Lease is verifying” | Tests/commands running — wait or re-run verify |
| “Ready for review” | Worker done — **your** turn to read diff and merge |
| “Local-wins on sync” | If you changed a card in JoyZoning, import won’t overwrite your status |
| “Smart setup” | App installs/connects Hermes automatically |
| “Profile joyzoning” | Separate Hermes settings folder so JoyZoning does not break your personal Hermes |

---

## Next

- [Desktop menu guide](desktop-menu-guide.md) — where to click  
- [What's next](whats-next.md) — first full workflow  
- [Technical glossary](../glossary.md)

[← Onboarding hub](README.md)
