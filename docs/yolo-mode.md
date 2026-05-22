# YOLO mode — supervised autopilot

YOLO mode lets agents **autonomously operate `jz` inside a pre-approved policy envelope**. It does **not** bypass governance.

> **Core invariant:** Agents may drive execution. Agents may **not** become final authority.

Humans still own **merge** and **Complete**. YOLO never calls `task complete`, `lease/merge`, or `raw`.

## What YOLO is

| YOLO **is** | YOLO **is not** |
|-------------|-----------------|
| Autonomous task pickup within policy | Autonomous authority |
| Dispatch / dispatch-retry of eligible low/medium risk work | Critical dispatch without prior human approval |
| Reopen blocked leases when `allowRecover: true` | Revoke (`allowRevoke` must stay `false`) |
| Executor settle wait + heartbeat while lease warms up | Skipping verification gates |
| Heartbeat, verify, retry verification | Marking tasks Complete |
| `ready_for_review` after passing checks | Human merge |
| Blocked + evidence on failure | Editing outside assigned worktree |
| Per-task + run audit evidence (`yolo.*`) | Changing runtime policy or `raw` API calls |

Think of it as **supervised autopilot**: the control plane keeps leases, worktrees, verification, and human merge gates intact.

## Commands

```bash
# Dry-run: which tasks would run (read-only list/lease queries)
jz yolo plan --policy .joyzoning/yolo.policy.json

# Execute within policy (requires explicit confirmation)
jz yolo run --policy .joyzoning/yolo.policy.json --yes

# Graceful stop (checked between tasks and during executor wait)
jz yolo stop

# Run state
jz yolo status
```

| File | Purpose |
|------|---------|
| `~/.joyzoning/yolo-run.state.json` | Active run phase, counters, stop flag |
| `~/.joyzoning/yolo-runs/{runId}.jsonl` | Append-only run audit log (all `yolo.*` session events) |

## Policy file (`YoloPolicy`)

Copy `.joyzoning/yolo.policy.example.json` and set `sessionId`.

```json
{
  "enabled": true,
  "sessionId": "00000000-0000-0000-0000-000000000001",
  "maxRiskLevel": "medium",
  "allowedTaskTags": ["docs", "test"],
  "forbiddenTaskTags": ["infra", "prod"],
  "maxTasksPerRun": 3,
  "maxRuntimeMinutes": 45,
  "maxVerificationRetries": 2,
  "dispatchSettleTimeoutSeconds": 600,
  "dispatchPollIntervalSeconds": 5,
  "heartbeatIntervalSeconds": 45,
  "allowRecover": true,
  "allowRevoke": false,
  "requireCleanWorktreeBeforeStart": true,
  "mergeHandoffVerificationCommands": true,
  "requiredVerificationCommands": ["dotnet build", "dotnet test"],
  "forbiddenCommands": ["git push", "git reset --hard", "rm -rf", "sudo "],
  "requireHumanMerge": true
}
```

| Field | Purpose |
|-------|---------|
| `enabled` | Must be `true` to run |
| `sessionId` | Session whose tasks are scanned |
| `maxRiskLevel` | `low` or `medium` only |
| `allowedTaskTags` | If non-empty, task must have at least one tag |
| `forbiddenTaskTags` | Tasks with any listed tag are skipped |
| `maxTasksPerRun` | Stop after N successfully processed picks |
| `maxRuntimeMinutes` | Wall-clock budget for the run |
| `maxVerificationRetries` | Retries after first verification failure (`0` = one attempt) |
| `dispatchSettleTimeoutSeconds` | Wait for lease → Running before verify |
| `dispatchPollIntervalSeconds` | Poll interval during executor wait |
| `heartbeatIntervalSeconds` | Heartbeat cadence during executor wait |
| `allowRecover` | Pick up `Blocked` tasks; `ReopenBlocked` + `dispatch-retry` |
| `allowRevoke` | **Must be `false`** (policy load rejects `true`) |
| `requireCleanWorktreeBeforeStart` | `git status --porcelain` empty in worktree |
| `mergeHandoffVerificationCommands` | Union handoff packet commands with policy list |
| `requiredVerificationCommands` | Run locally in worktree before `ready_for_review` |
| `forbiddenCommands` | Substrings forbidden in verification commands |
| `requireHumanMerge` | Must stay `true` (enforced at load) |

### Task tags

Tags are parsed from task **title/description**:

- Hashtags: `#docs` `#test`
- Line: `tags: docs, test`

## Execution loop

1. Load and validate policy (`allowRevoke` / `requireHumanMerge` enforced)
2. List session tasks; evaluate eligibility (risk, status, tags, active lease)
3. For each selected task (until limits or stop):
   - **Blocked + `allowRecover`:** `lease/recover` (`ReopenBlocked`) → `dispatch-retry`
   - **Else:** `dispatch` (never sets `humanApprovedCritical`)
   - Poll lease until `Running` / `Verifying` / `Blocked` / `ReadyForReview` (or timeout)
   - Heartbeat during wait
   - Bind worktree context
   - Run merged verification commands with retries
   - On exhaustion → `blocked` + `yolo.blocked`
   - On pass → submit verification → `ready_for_review` + `yolo.ready_for_review`
4. Stop on policy violation, unexpected API error, `jz yolo stop`, or time/task limits
5. Write final line to `~/.joyzoning/yolo-runs/{runId}.jsonl`

## Evidence events

| Kind | When |
|------|------|
| `yolo.started` | Run begins (audit log + first affected tasks) |
| `yolo.task.selected` | Task chosen for processing |
| `yolo.task.skipped` | Ineligible / critical / already in review |
| `yolo.dispatch.attempted` | Fresh dispatch |
| `yolo.recover.attempted` | `ReopenBlocked` before retry |
| `yolo.dispatch.retry.attempted` | `dispatch-retry` after recover |
| `yolo.executor.wait` | Lease ready for local verification |
| `yolo.verify.retry` | Verification failed, retrying |
| `yolo.blocked` | Verification exhausted |
| `yolo.ready_for_review` | Passing verification submitted |
| `yolo.stopped` | Run ended |
| `yolo.policy.violation` | Policy rule broken |

## Recommended safe policies

**Docs / tests only (safest):**

```json
{
  "maxRiskLevel": "low",
  "allowedTaskTags": ["docs", "test"],
  "maxTasksPerRun": 1,
  "maxRuntimeMinutes": 30,
  "maxVerificationRetries": 1,
  "allowRecover": false,
  "requiredVerificationCommands": ["dotnet build"]
}
```

Always run `jz yolo plan` before `jz yolo run --yes`.

## Anti-goals (forbidden actions)

YOLO must **never**:

- Call `jz task complete` or merge endpoints
- Call `jz raw`
- Dispatch high/critical tasks without human `--approve-critical` on the operator shell
- Revoke leases (`allowRevoke` cannot be enabled in policy)
- Edit files outside the dispatched worktree
- Mutate control-plane runtime policy

After YOLO finishes:

```bash
jz task complete <taskId> --yes   # human merge gate only
```

## Governance preserved

| Mechanism | YOLO behavior |
|-----------|---------------|
| Lease authority | Normal lease per task |
| Worktree isolation | Dispatch creates isolated worktree |
| Verification gates | Required + handoff commands must pass |
| Evidence logging | `yolo.*` on lease + JSONL audit |
| Stale lease recovery | Background reconciliation unchanged |
| Critical approval | High/critical skipped; no `humanApprovedCritical` from YOLO |
| Human merge for Complete | `requireHumanMerge` always true |

## Related docs

- [cli.md](cli.md) — operator vs agent harness
- [execution-orchestration-api.md](execution-orchestration-api.md) — REST contract
- [concepts.md](concepts.md) — authority model
