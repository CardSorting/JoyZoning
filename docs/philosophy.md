# JoyZoning development philosophy

JoyZoning is built around a small set of ideas that show up in the README, control plane, CLI, and docs. If a feature or doc contradicts these, the feature is wrong — not the philosophy.

**Related:** [concepts.md](concepts.md) (technical spine) · [jsdp.md](jsdp.md) (sequential delivery) · [workspace-state.md](workspace-state.md) (one card → one truth) · [what-is-joyzoning.md](what-is-joyzoning.md) (plain language)

---

## 1. Canonical workspace — one repo, one truth

Agents do not get a hidden copy of your project.

| Old mental model | Current model |
|------------------|---------------|
| Sandbox under `.joyzoning/worktrees/<id>/` | **Session workspace** — the folder you opened |
| Live mirror under `.joyzoning/live/` | **Same path** — git branch distinguishes active card |
| “Which folder is real?” | **Always** `OperatorSession.WorkspaceRoot` |

After **dispatch**, work happens on branch `joyzoning/card-<task-id>` in that root. Workspace, `jz task watch`, Watch UI, and timeline events all inspect **that path**. Legacy `worktrees/` and `live/` folders are pruned on merge/revoke — they are not part of the execution model.

**Why:** Shadow copies drift from what you merge, waste disk, and break sequential delivery chains where each role must extend the same codebase.

---

## 2. JSDP by default — line dance, not jazz band

Multi-role delivery uses the **JoyZoning Sequential Delivery Protocol (JSDP)**:

- One **bounded session** per role (eight in a standard chain).
- **One execution per role** — either a managed Hermes lease **or** an external agent path (Cursor, Claude Code, manual) with **no lease**.
- **Shared canonical workspace** — each role builds on accepted work from the previous role.
- **Mandatory accept-merge** before the next role starts.

Parallel agents rewriting the same tree from empty sandboxes produce **code soup**. JSDP trades fake speed for **stable convergence**.

| Path | Who edits files | JoyZoning still enforces |
|------|-----------------|---------------------------|
| Managed (`TaskExecutionMode.ManagedAgent`) | Hermes / DietCode via lease | Lease lifecycle, verify, merge |
| External (`TaskExecutionMode.ExternalAgent`) | Cursor, Claude Code, Copilot, you | Branch, prompt, mark-ready, verify, complete |

Operators: [jsdp.md](jsdp.md) · [external-agent-jsdp.md](external-agent-jsdp.md) · `./scripts/role-chain-dispatch.sh` · `jz delivery-chain next --external`

---

## 3. Cognition vs authority

| Layer | Role | Examples |
|-------|------|----------|
| **Cognition** | Planning, reasoning, tools in conversation | Manager Chat, Hermes TUI, `hermes --tui` |
| **Authority** | What may become project state | Verification evidence, `ready_for_review`, **human merge** |

Chat can say “done.” **Workspace + verification + merge** decide what ships. Agents never set `WorkTaskStatus.Complete` or call merge — desktop, REST, `jz`, and `jz agent` share the same rule.

---

## 4. One card → one inspection context

Pick a kanban card → every surface resolves **one folder and one git view** for that card:

- Undispatched: session workspace on your default branch.
- Dispatched: session workspace on `joyzoning/card-<id>`.

Same contract as **GitHub PR → Files changed**: one ticket, one diff, one sign-off. Details: [workspace-state.md](workspace-state.md).

---

## 5. Evidence over vibes

- Verification commands run in the **task workspace**; results attach to the lease.
- Failed runs stay visible in the evidence log.
- Merge requires a **passing** verification report (unless you explicitly override policy).

“The model said it worked” is not a release criterion.

---

## 6. One policy, many surfaces

Desktop, Watch UI, REST (`:9470`), SignalR, and `jz` / `jz agent` call the same orchestrator. There is no back door to **Complete**. When docs describe an operator action, the CLI and API names should match.

Discover APIs via manifest — do not guess routes:

```bash
./scripts/joyzoning agent-manifest --json
./scripts/joyzoning endpoints --json
```

---

## 7. Complement, don’t replace — cockpit, optional engines

JoyZoning is an **operator cockpit**, not an IDE and not a second Hermes install.

- **Hermes** is optional — use it when you want a managed worker lease and tool loop inside JoyZoning.
- **Cursor, Claude Code, Copilot, and manual edits** are first-class — use `jz task start-external` or `jz delivery-chain next --external` so JoyZoning still owns branch, task state, verification, and merge.
- The **repo and merge gate** are the authority; chat and external IDEs only change files.

See [external-agent-jsdp.md](external-agent-jsdp.md).

---

## 8. Recoverability without orphan sandboxes

Revoke, block, and recovery flows preserve **git state and evidence** on the lease. They do not depend on resurrecting deleted sandbox directories — because there are no sandboxes. Recovery reopens or replaces leases against the canonical workspace.

---

## How this shows up in the repo

| Concern | Where it lives |
|---------|----------------|
| Branch + path planning | `WorktreePlanner`, `JsdpWorkspaceExecution` |
| Workspace inspection | `WorkspaceInspection`, `GET /api/tasks/{id}/workspace/*` |
| Live updates | `WorkspaceEventPublisher` → SignalR `OnWorktreeRefreshed`, `OnTaskLiveUpdated` |
| Sequential chains | Delivery chain API, external-agent JSDP, `role-chain-dispatch.sh` |
| Agent entry | [AGENTS.md](../AGENTS.md), [jsdp.md](jsdp.md) |

---

## Read next

| Audience | Doc |
|----------|-----|
| New operators | [what-is-joyzoning.md](what-is-joyzoning.md) · [execution-paths.md](execution-paths.md) |
| Research / leaders | [whitepaper.md](whitepaper.md) (v1.5, C1–C8) · [whitepaper-summary.md](whitepaper-summary.md) |
| JSDP chains | [jsdp.md](jsdp.md) · [external-agent-jsdp.md](external-agent-jsdp.md) |
| Modes (plan / execute / review) | [operational-modes.md](operational-modes.md) |
| Contributors | [development.md](development.md) |
| Coding agents | [AGENTS.md](../AGENTS.md) |
