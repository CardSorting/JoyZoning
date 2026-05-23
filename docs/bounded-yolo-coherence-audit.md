# Bounded YOLO + real git convergence — coherence audit

**Date:** May 2026  
**Scope:** Stabilize existing system (no redesign).  
**Goal:** Prove the goblin court cannot forge reality paperwork — `Merged` and `Complete` track real workspace state.

---

## Critical issues (fixed in this pass)

| Issue | Risk | Resolution |
|--------|------|------------|
| **`RepairMergedTaskActiveLeaseAsync` metadata-forged `Merged`** | Task `Complete` + lease `ReadyForReview` → reconciliation set `Merged` with **no** `git.convergence.succeeded` | Only align lease to `Merged` when prior git success evidence exists; otherwise `Blocked` with explicit repair message |
| **Autopilot used `Human` actor on git/lease evidence** | Audit trail implied manual accept for autopilot runs | `AcceptResultAsync(cardId, StatusChangeActor actor)` — autopilot/reconcile pass `System` |
| **Race: double accept after git** | Two accepts could both converge then fight on metadata | Persist `git.convergence.succeeded` **before** `Merged`; re-load lease; reject if no longer `ReadyForReview` |
| **Stale docs: “metadata accept”** | Operators/docs believed merge API was metadata-only | `docs/worker-convergence.md` updated; default `MetadataOnlyAcceptResult: false` in appsettings |

---

## High-priority issues (fixed or mitigated)

| Issue | Mitigation |
|--------|------------|
| **UI “Merged” without git proof** | `convergence.ts` warns when lease is Merged but no `git.convergence.succeeded` on readiness |
| **Autopilot accept evidence thin** | `authority.auto_accepted` detail now includes full `GitConvergenceEvidence.ToEvidenceDetail` |
| **Reconciliation evidence spam** | Fingerprint dedupe on `authority.decision` / `authority.auto_blocked` (prior pass) |
| **Silent reconciliation failures** | `LogWarning` on authority reconcile failure; hosted service logs authority eval counts |

---

## Authority invariants (verified)

| Invariant | Status |
|-----------|--------|
| `Merged` only after successful git (default config) | **Yes** — `AcceptResultAsync` gates on `convergence.Succeeded` unless `MetadataOnlyAcceptResult` |
| Task `Complete` only via accept path after merge transition | **Yes** — `ApplyTaskStatusAsync(Complete)` in `AcceptResultAsync`; agent forbidden |
| `MetadataOnlyAcceptResult` off by default | **Yes** — `LeaseRuntimeOptions` default `false`; `appsettings.json` explicit `false` |
| Autopilot blocks when `MetadataOnlyAcceptResult` | **Yes** — `AuthorityReasonCodes.MetadataOnlyMode` |
| Manual + autopilot share convergence | **Yes** — both call `AcceptResultAsync` → `IWorkspaceGitMerger` |
| Autopilot safety gates | **Yes** — ReadyForReview, verification, files, protected paths, conflicts, overlaps, unknown observability |
| Reconciliation cannot override protected-path block | **Yes** — re-evaluates policy each tick; protected paths still block auto-accept |

---

## Race conditions (assessed)

| Scenario | Behavior |
|----------|----------|
| Autopilot + manual accept | Second call: **404** (no active lease) **or** **409** (lease no longer `ReadyForReview` after git evidence persisted) |
| Reconciliation + verification | Both may call `TryAutoAccept`; dedupe + post-git lease check limit double metadata merge |
| Two reconciliation ticks | Evidence idempotent via fingerprint |
| Two ready workers, same files | Overlap → `merge_conflict` / `overlapping_ready_worker`; autopilot blocked |

**Remaining:** No DB row-version lock; extreme races (two accepts before first `UpdateAsync`) are unlikely in single control plane but not impossible. Mitigation: persist git evidence immediately after convergence.

---

## Failure ordering (operator visibility)

| Failure | Code entered main? | Metadata `Merged`? | Evidence |
|---------|-------------------|-------------------|----------|
| Git convergence fails | No | No | `git.convergence.failed` + lease stays `ReadyForReview` |
| Git succeeds, lease no longer ready | **Yes** (git ran) | No | `git.convergence.succeeded` persisted; 409 on merge step |
| Git succeeds, `UpdateAsync` fails | Likely yes | Maybe not | Git evidence in last successful write attempt |
| `Merged` set, task `Complete` fails | Yes | Yes | Lease `Merged`; task status repair via reconciliation |
| Task `Complete`, lease `ReadyForReview`, no git | Unknown | No (after fix) | Lease **Blocked** + `reconciliation.repair`; operator runs Accept result |
| Worktree missing | No | No | 409 before git |
| Dirty destination (default) | No | No | Convergence fails with dirty file list |

**Operator UI:** Convergence panel + authority activity + `BlockedReason` on lease.

---

## Observability / evidence

| Event | Kind | Includes |
|-------|------|----------|
| Policy evaluate | `authority.decision` | profile, risk, reasonCodes, files, fingerprint |
| Autopilot block | `authority.auto_blocked` | overlap paths, mergeState, messages |
| Autopilot accept | `authority.auto_accepted` + `git.convergence.succeeded` + `lease.merged` | full git detail, profile |
| Manual accept | `git.convergence.succeeded` + `lease.merged` | actor `Human`, full git detail |
| Git failure | `git.convergence.failed` | strategy, conflicts, destination heads |

---

## Operator UI clarity

| Term | Meaning |
|------|---------|
| **Accept result** | Git convergence + `Merged` + `Complete` |
| **Needs review** | Policy-blocked or conflicts (embedded merge queue) |
| **Review conflict** | Inspect overlap/git conflict (`inspect` action) |
| **Revoke** | Abandon worker |
| **Autopilot · bounded YOLO** | Session authority profile + activity feed |
| **Reconcile pending** | Manual `POST …/authority/reconcile` |

API path remains `/lease/merge` (stable contract); labels say **Accept result**.

---

## Tests added (this pass)

- `Second_accept_after_merged_fails_with_conflict`
- `Autopilot_accept_records_system_actor_on_git_convergence`
- `Reconciliation_does_not_metadata_merge_ready_lease_without_git_evidence`
- (Prior) overlap block, evidence dedupe, autopilot low-risk accept, authority policy suite

---

## Files changed (this pass)

- `KanbanExecutionOrchestrator.cs` — actor param, post-git lease check, persist git before `Merged`
- `LeaseRuntimeService.cs` — repair merge requires git evidence; System actor on reconcile accept
- `AuthorityAutopilotService.cs` — full git detail on auto-accept
- `ApiEndpoints.cs` — reconcile uses `System` accept
- `docs/worker-convergence.md`, `docs/bounded-yolo-coherence-audit.md`
- `web/watch/src/lib/operator-labels.ts`, `convergence.ts`
- `tests/.../KanbanExecutionOrchestratorTests.cs`, `LeaseRuntimeServiceTests.cs`

---

## Intentional limitations (unchanged)

- **API route** `/lease/merge` — not renamed (clients/CLI).
- **`ApproveMergeAsync`** — alias kept.
- **`MetadataOnlyAcceptResult`** — dev escape hatch; must stay off in production.
- **No distributed lock** on accept — single-node control plane assumption.
- **Preflight `action=approve`** — name legacy; maps to accept-result guardrails.
- **Parallel mode tabs** — still say “merge queue” in places; operator console is canonical flat surface.

---

## Sign-off checklist

- [x] No production path marks `Merged` without git success (except explicit metadata-only flag).
- [x] Reconciliation cannot paper-merge orphaned Complete+ReadyForReview without git proof.
- [x] Autopilot and manual share `AcceptResultAsync` / `WorkspaceGitMerger`.
- [x] Double accept does not double-complete in normal flow.
- [x] Evidence distinguishes System vs Human accept.
- [x] Docs match behavior.
