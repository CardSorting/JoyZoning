# Worker worktree convergence

How parallel worker sandboxes relate to the **canonical session workspace**, what **accept result** (`POST …/lease/merge`) does today, and where conflicts appear.

**UI terminology:** Watch calls this **Accept result**, not “Approve merge”, until [real-git-convergence.md](real-git-convergence.md) is implemented.

## Mental model

```text
MAIN WORKSPACE (session.WorkspaceRoot)
  The real project checkout JoyZoning binds to the operator session.

WORKER WORKTREES ({root}/.joyzoning/worktrees/{segment})
  Temporary sandboxes where Hermes/DietCode runs per card lease.

MERGE QUEUE (GET /api/sessions/{id}/merge-queue)
  Read model: ready / conflict / completed / revoked buckets + mergeReadiness.

REVIEW (Watch console + decision-preflight)
  Human gate before approve/revoke.

ACCEPT RESULT (POST /api/tasks/{taskId}/lease/merge)
  JoyZoning lease + task status transition — see §6 (not a git merge today).

REVOKE (POST /api/tasks/{taskId}/lease/revoke)
  Lease → Revoked; worktree and mirror paths are preserved for inspection.
```

```text
Worker A worktree ──┐
Worker B worktree ──┼──> Merge queue ──> Human review ──> Main workspace (canonical)
Worker C worktree ──┘         ▲                              ▲
                              │                              │
                         observability                  metadata on approve;
                         + git diff in worktree          git merge not automated
```

## Answers (audit checklist)

### 1. Where is the canonical main workspace?

`OperatorSession.WorkspaceRoot` — exposed on workspace snapshot as `sessionWorkspaceRoot` and on merge/parallel APIs as `sessionWorkspaceRoot`.

This is the session’s primary checkout. JSDP executes directly in this root on branch `joyzoning/card-<task-id>` (no `.joyzoning/worktrees/` sandbox).

**Code:** `WorktreePlanner.TryPlan(workspaceRoot, …)` takes `session.WorkspaceRoot` as `workspaceRoot`.

### 2. Where is each worker worktree?

JSDP uses the **canonical workspace**:

`ExecutionLease.WorktreePath` = `OperatorSession.WorkspaceRoot`

Branch: `joyzoning/card-{segment}` where `segment` is derived from the card id or Hermes kanban id.

**Code:** `WorktreePlanner.cs`, `JsdpSessionPolicy.UseCanonicalWorkspace`, `KanbanExecutionOrchestrator.cs`.

### 3. What branch/commit/diff does each worker produce?

| Field | Source |
|--------|--------|
| Branch | `ExecutionLease.BranchName` — default `joyzoning/card-{segment}` |
| Head | `git rev-parse HEAD` in worktree (`GitWorkspaceStatus.TryGetWorktreeSummaryAsync`) |
| Base | `git merge-base {mainHead} {worktreeHead}` when main root is a git repo |
| Changed files | `git status --porcelain` in worktree; also verification report `changedFiles` |

Exposed on merge queue / parallel workers as `mergeReadiness` (`headCommit`, `baseCommit`, `changedFilesSummary`, `changedFilesCount`, `isDirty`).

**Code:** `GitWorkspaceStatus.cs`, `WorkerMergeObservabilityBuilder.cs`.

### 4. What moves a worker to `ready_to_merge`?

1. Agent run completes verification phase (`ExecutionLeaseStatus.Verifying`).
2. DietCode submits a **passing** verification report: `POST /api/tasks/{id}/verification` (or supersede variant).
3. Lease → `ReadyForReview`; task → `NeedsApproval`.
4. `WorkerMergeStateResolver` returns `ready_to_merge` when `ValidateHumanMerge(lease)` passes (passing report on lease, no git unmerged paths, no overlapping ready-worker files).

**Code:** `KanbanExecutionOrchestrator.SubmitVerificationAsync`, `WorkerMergeStateResolver.cs`, `KanbanExecutionRules.ValidateHumanMerge`.

### 5. What API accepts the result?

`POST /api/tasks/{taskId}/lease/merge` → `KanbanExecutionOrchestrator.AcceptResultAsync` (alias `ApproveMergeAsync`).

Preflight (optional): `GET /api/sessions/{sessionId}/workers/{executionSessionId}/decision-preflight?action=accept` (`approve` is a legacy alias).

Real git convergence runs **before** lease `Merged` unless `LeaseRuntime:MetadataOnlyAcceptResult` is true (dev-only). See [real-git-convergence.md](real-git-convergence.md).

### 6. What happens on accept result?

**Default (`LeaseRuntime:MetadataOnlyAcceptResult` = false):** `AcceptResultAsync` runs `IWorkspaceGitMerger` first:

1. Squash-merge `lease.BranchName` into `session.WorkspaceRoot` when the branch exists in the canonical repo.
2. Else patch-apply `base..head` from a git-capable worktree.
3. Else copy files from the worktree directory into the canonical root and commit.

On success: evidence `git.convergence.succeeded` with source/destination commits and changed files, then lease `Merged` + task `Complete`.

On failure: evidence `git.convergence.failed`, lease stays `ReadyForReview`, API returns **409** — **no** `Merged` metadata.

**Dev-only fallback:** `MetadataOnlyAcceptResult: true` skips git (legacy metadata accept).

**Code:** `KanbanExecutionOrchestrator.AcceptResultAsync`, `WorkspaceGitMerger.cs`.

### 7. Where are conflicts detected?

| Kind | Detector |
|------|-----------|
| Git unmerged paths | `GitWorkspaceStatus` — porcelain `unmerged` / conflict paths in worktree |
| Overlapping ready workers | `WorkerMergeObservabilityBuilder` — file path sets intersect across `ReadyForReview` leases |
| Merge preconditions | `WorkerMergeStateResolver` — failed verification, `ValidateHumanMerge` errors → `merge_conflict` |
| Execution ended abnormally | `execution_ended` category when phase failed/cancelled |

**Code:** `WorkerMergeObservabilityBuilder.cs`, `WorkerMergeStateResolver.cs`, `OperatorDecisionSafety.cs`.

### 8. Where are conflict files surfaced?

- `GET …/merge-queue` → `mergeConflict.conflictFiles` per worker.
- `GET …/decision-preflight?action=accept|inspect` → `summary.riskFlags`, blocks/warnings (`approve` aliases `accept`).
- Watch **Convergence** panel lists the same paths for the selected worker.

### 9. After approve, how to verify main workspace changed?

**JoyZoning state:** task `Complete`, lease `Merged`, merge queue bucket `completedWorkers`, `mergeState: merged`.

**Git main workspace:** not updated by approve today — verify with `git status` / `git log` in `sessionWorkspaceRoot` yourself. Watch shows an explicit note on the Convergence panel.

### 10. Rejected / revoked worker output

`POST /api/tasks/{taskId}/lease/revoke`:

- Lease → `Revoked` (terminal).
- Evidence records preserved `WorktreePath`, `BranchName`, verification JSON.
- **Does not delete** worktree or mirror paths (`RevokeLeaseAsync` detail + `OperatorDecisionSafety` warnings).
- Mirror lifecycle completed with `WorkerMergeState.Revoked`.

Abandoned/stale workers: resolver + mirror lifecycle; paths may remain until manual cleanup / GC policies.

## Key files

| Area | File |
|------|------|
| Worktree planning | `src/JoyZoning.Domain/Orchestration/WorktreePlanner.cs` |
| Lease lifecycle | `src/JoyZoning.ControlPlane/Services/KanbanExecutionOrchestrator.cs` |
| Merge rules | `src/JoyZoning.Domain/Orchestration/KanbanExecutionRules.cs` |
| Merge state | `src/JoyZoning.Domain/Orchestration/WorkerMergeStateResolver.cs` |
| Observability | `src/JoyZoning.ControlPlane/Services/WorkerMergeObservabilityBuilder.cs` |
| Git diff | `src/JoyZoning.Adapters/Workspace/GitWorkspaceStatus.cs` |
| HTTP | `src/JoyZoning.ControlPlane/Endpoints/ApiEndpoints.cs` |
| Watch UI | `web/watch/src/lib/convergence.ts`, `web/watch/src/components/ConvergencePanel.tsx` |
