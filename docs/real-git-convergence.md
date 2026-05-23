# Real git convergence on accept (design)

**Status:** implemented (May 2026) — default path runs git convergence before metadata `Merged`. Set `LeaseRuntime:MetadataOnlyAcceptResult: true` for legacy behavior. See [worker-convergence.md](worker-convergence.md).

## Rule

```text
No metadata "Merged" unless code entered the canonical workspace.
```

Until that holds, the Watch UI calls the action **Accept result**, not “Approve merge”.

## Target: `ApproveMergeAsync` (rename internally optional)

1. **Verify lease** — `ReadyForReview`, passing verification on lease (`ValidateHumanMerge`).
2. **Verify worktree** — path exists; policy for dirty tree (allow only expected dirty files vs block).
3. **Compute merge plan** — source: `lease.WorktreePath`, branch `lease.BranchName`, HEAD; destination: `session.WorkspaceRoot`, target branch (config or current HEAD branch).
4. **Execute** — `git merge` or `git merge --squash` (product choice) worker branch into destination checkout; alternative: cherry-pick range `base..head`.
5. **Conflicts before metadata** — if git exits non-zero or conflict markers present, return **409** with conflict paths; lease stays `ReadyForReview`.
6. **Metadata only after success** — lease `Merged`, task `Complete`, mirror `merged`.
7. **Evidence** — append structured detail:
   - `sourceWorktree`, `sourceBranch`, `sourceCommit`
   - `destinationRoot`, `destinationBranch`
   - `mergeCommit` or `squashCommit`
   - `changedFiles` (post-merge diff stat vs pre-merge main HEAD)
8. **Preserve sandboxes** — do not delete worktree/mirror (same as revoke policy).

## API / UI follow-ups

| Surface | Change |
|---------|--------|
| Watch primary action | Already **Accept result** until step 6 ships |
| Success response | Include `mergeCommit`, `destinationBranch` |
| Convergence panel | Replace warning with merge commit + verify hint |
| Failed git merge | `mergeState: merge_conflict`, lease unchanged |

## Implementation sketch

- New port: `IWorkspaceGitMerger` in `JoyZoning.Adapters.Workspace` (wrap `git` like `GitWorkspaceStatus`).
- `KanbanExecutionOrchestrator.ApproveMergeAsync` orchestrates: preflight git → merge → evidence → DB.
- Tests: temp repo with two branches, accept succeeds → main has commits; conflict case → lease still `ReadyForReview`.

## Open decisions

- **Merge vs squash** — operator config per session or global default?
- **Non-git workspaces** — reject accept with clear error, or file-copy fallback?
- **Parallel overlap** — still block in observability before git attempt?
