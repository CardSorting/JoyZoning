# External-agent JSDP

JoyZoning supervises **JSDP delivery** even when it does not run the agent. Hermes, Cursor, Claude Code, Copilot, terminal scripts, and manual edits are all valid ways to change files — JoyZoning still owns task state, branch state, verification, review, and merge.

**Core protocol:** [jsdp.md](jsdp.md) · **CLI:** [cli.md](cli.md) · **Philosophy:** [philosophy.md](philosophy.md)

---

## Cockpit vs engines

| Layer | Owns | Does not own |
|-------|------|----------------|
| **JoyZoning** | Task status, branch, workspace scan, prompts, verification evidence, review gate, merge gate, JSDP chain order | Editing source files |
| **External tool** | Files in the repo (Cursor, Claude Code, Copilot, manual, scripts) | Marking tasks Complete, skipping merge, dispatching the next role |

```text
JoyZoning = cockpit
Hermes      = optional engine (ManagedAgent)
Cursor      = optional engine (ExternalAgent)
Claude Code = optional engine (ExternalAgent)
Manual      = optional engine (ExternalAgent)
Repo + merge gate = authority
```

---

## Execution model

### `TaskExecutionMode`

| Mode | Meaning |
|------|---------|
| `ManagedAgent` | JoyZoning dispatches a Hermes/DietCode lease and runs the worker via the control plane |
| `ExternalAgent` | Operator or external IDE edits the canonical workspace; **no Hermes lease** |

### `ExecutionDriver`

| Driver | Typical use |
|--------|-------------|
| `Hermes` | Managed dispatch (`jz task dispatch`, `jz task run`) |
| `ExternalCursor` | `jz task start-external --agent cursor` |
| `ExternalClaudeCode` | `--agent claude-code` |
| `ExternalCopilot` | `--agent copilot` |
| `ExternalManual` | `--agent manual` |
| `ExternalCustom` | Any other `--agent <name>` |

### Task statuses (external path)

| Status | Meaning |
|--------|---------|
| `Planned` | Role task exists; work not started |
| `ExternalInProgress` | External work started; branch checked out |
| `ReadyForReview` | Operator marked work ready (`jz task mark-ready`) |
| `Verified` | Verification commands passed |
| `Complete` | Operator accept-merge (`jz task complete --yes`) |
| `Blocked` | Verification failed or operator blocked |
| `Failed` | Terminal failure state |

Managed leases still use lease statuses (`Leased` → `Running` → `ReadyForReview` → `Merged`). External roles converge when `Complete` **and** `ExternalMergeCompleted` is set (no `Merged` lease required).

---

## External role lifecycle

```text
jz task start-external <task-id> --agent cursor
  → branch joyzoning/card-<task-id>
  → prompt generated (JSDP rules + role scope)
  → status ExternalInProgress
  → NO Hermes lease

[Edit in Cursor / Claude Code / terminal / by hand]

jz task mark-ready <task-id>
  → status ReadyForReview (requires branch match + changes)

jz task verify <task-id> --cmd "npm test"
  → evidence attached; status Verified on pass

jz task complete <task-id> --yes
  → git convergence (when enabled) + ExternalMergeCompleted
  → status Complete
  → JSDP gate opens for next role
```

Agents must **not** call `PUT /status → Complete`. The merge gate blocks raw Complete for bounded-role and external tasks.

---

## JSDP chain (external dispatch)

```bash
jz delivery-chain create --program "My Program" --workspace /path/to/project

jz delivery-chain next <chain-id> --external --agent cursor
# Prints: role name, task id, branch, workspace path, copyable prompt, next human action

jz delivery-chain queue <chain-id>
# Role 2 blocked until Role 1 is Complete (external merge gate satisfied)
```

Equivalent API: `POST /api/delivery-chains/{id}/next-external` with body `{ "agent": "cursor" }`.

---

## Generated prompt

External start builds a copyable prompt (also via `jz task prompt <task-id>`) containing:

- Project name and role title
- Workspace path and branch name
- Role scope and allowed areas
- Product Lock and Architecture Lock references
- JSDP rules and stop conditions
- Required **seven-section** output (Goal, Scope, Planned Changes, Risks, Deliverables, Completion Criteria, Follow-Up Notes)
- Explicit instruction: **do not mark the task complete** — operator reviews, verifies, and merges

---

## Workspace scanning

`GET /api/tasks/{id}/workspace/status` returns:

- Current branch vs expected `joyzoning/card-<task-id>`
- Dirty / staged / untracked / changed files
- Last commit and last scan time
- `executionDriver`, `taskExecutionMode`, `status`
- `readyForReviewAllowed` and `blockedReason`

CLI: `jz task status <task-id> --refresh`

---

## API reference (external)

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/api/tasks/{id}/external/start` | Start external work (`{ "agent": "cursor" }`) |
| `GET` | `/api/tasks/{id}/external/prompt` | Read generated prompt |
| `GET` | `/api/tasks/{id}/external/status` | Task + optional workspace refresh |
| `GET` | `/api/tasks/{id}/workspace/status` | Git scan + driver/mode |
| `POST` | `/api/tasks/{id}/external/ready-for-review` | Operator marks ready |
| `POST` | `/api/tasks/{id}/verification` | Same as managed; routes to external when `TaskExecutionMode` is external |
| `POST` | `/api/tasks/{id}/external/complete` | Accept-merge + complete (`operatorApproved: true`) |
| `POST` | `/api/delivery-chains/{id}/next-external` | Next eligible role as external |
| `GET` | `/api/delivery-chains/{id}/prompt` | Prompt for active/next chain role |

Discover via `joyzoning endpoints --json` (registry ids: `task-external-*`, `delivery-chain-next-external`).

---

## CLI quick reference

```bash
# Single card
jz task start-external <task-id> --agent cursor
jz task prompt <task-id>
jz task status <task-id> [--refresh]
jz task mark-ready <task-id>
jz task verify <task-id> --cmd "npm test"
jz task complete <task-id> --yes

# Delivery chain
jz delivery-chain create --program "TinyQuest" --workspace /path/to/repo
jz delivery-chain next <chain-id> --external --agent cursor
jz delivery-chain prompt <chain-id>
jz delivery-chain queue <chain-id>
```

`jz task complete` detects `TaskExecutionMode.ExternalAgent` and calls the external complete API automatically.

---

## Enforcement (unchanged gates)

- **No Hermes lease** for `ExternalAgent` tasks
- **Branch** `joyzoning/card-<task-id>` on canonical workspace
- **Mark ready** requires branch match and workspace changes
- **Verify** before complete when `VerificationRequired`
- **Complete** requires operator `--yes` and prior review/verify gates
- **Next JSDP role** blocked until prior role `Complete` + external merge convergence
- **YOLO / plan / run** still blocked on bounded-role sessions — use `delivery-chain` or external start
- **Autopilot** cannot bypass external JSDP gates

Implementation: `ExternalTaskExecutionService`, `JsdpMergeGate.IsRoleConverged`, `JsdpSessionPolicy`, `RoleDeliveryChainGate`.

---

## Audit events

| Event | When |
|-------|------|
| `external.work.started` | External start |
| `external.agent.prompt_generated` | Prompt built |
| `external.workspace.status_scanned` | Workspace scan |
| `external.work.marked_ready_for_review` | Mark ready |
| `external.verification.started` / `passed` / `failed` | Verify |
| `external.work.merged` | Git convergence |
| `external.work.completed` | Task Complete |

Query: `GET /api/events?correlationId=<task-id>`

---

## UI

Watch / parallel workers panel shows:

- **Execution:** `External: Cursor` (or driver label)
- Branch, workspace path, last scan, changed files
- Actions: copy prompt, mark ready, verify, complete (same authority gates as desktop)

---

## When to use which path

| Situation | Path |
|-----------|------|
| Full Hermes tool loop in JoyZoning | `jz task dispatch` / `jz task run` (ManagedAgent) |
| You already work in **Cursor** on the repo | `jz task start-external --agent cursor` |
| **Claude Code** in terminal | `--agent claude-code` |
| You edit by hand, JoyZoning tracks state | `--agent manual` |
| JSDP 8-role chain, no Hermes per role | `jz delivery-chain next --external --agent cursor` |

---

## Acceptance example (TinyQuest)

```bash
jz delivery-chain create --program "TinyQuest Campfire" --workspace /path/to/tinyquest
jz delivery-chain next <chainId> --external --agent cursor
# → Role 1 task, branch, prompt, external_in_progress, no lease, Role 2 blocked

# Edit in Cursor, then:
jz task mark-ready <task-id>
jz task verify <task-id> --cmd "npm test"
jz task complete <task-id> --yes
# → Role 1 Complete, Role 2 eligible in queue
```
