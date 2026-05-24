import type { MergeWorkerEntry } from "@/lib/merge-queue";
import { mergeStateLabel, shortCommit } from "@/lib/merge-queue";
import type { ConsoleWorker } from "@/lib/operator-console";
import type { LiveTaskSnapshot } from "@/lib/types";

/** Shown when git convergence has not run yet (ReadyForReview). */
export const ACCEPT_RESULT_PENDING_SUMMARY =
  "Accept result applies this worker's changes into the main workspace via git, then marks the lease Merged.";

export const METADATA_ONLY_ACCEPT_SUMMARY =
  "Metadata-only accept (dev): records Merged/Complete without git convergence.";

export interface ConvergenceModel {
  mainWorkspacePath: string;
  workerWorktreePath: string | null;
  workerMirrorPath: string | null;
  workerBranch: string | null;
  headCommit: string | null;
  baseCommit: string | null;
  mergeStatus: string;
  mergeStatusDetail: string | null;
  conflictFiles: string[];
  destinationWorkspacePath: string;
  acceptOperation: string;
  readyToMergeGate: string;
  lastResult: string | null;
  changedFilesSummary: string[];
  changedFilesCount: number;
  codeEnteredMainWorkspace: boolean;
  destinationNewHead: string | null;
  convergenceStrategy: string | null;
  appliedFiles: string[];
}

function asMergeWorker(worker: ConsoleWorker | null): MergeWorkerEntry | null {
  if (!worker || !("mergeState" in worker)) return null;
  return worker as MergeWorkerEntry;
}

export function buildConvergenceModel(
  snapshot: LiveTaskSnapshot,
  worker: ConsoleWorker | null,
  sessionWorkspaceRoot?: string | null,
): ConvergenceModel {
  const main =
    sessionWorkspaceRoot?.trim() ||
    snapshot.sessionWorkspaceRoot?.trim() ||
    "—";

  const mergeWorker = asMergeWorker(worker);
  const readiness = mergeWorker?.mergeReadiness ?? worker?.mergeReadiness ?? null;
  const conflict = mergeWorker?.mergeConflict ?? worker?.mergeConflict ?? null;

  const workerWorktreePath =
    readiness?.worktreePath?.trim() ||
    worker?.worktreePath?.trim() ||
    snapshot.worktreePath?.trim() ||
    null;

  const workerMirrorPath = workerWorktreePath;

  const mergeState = mergeWorker?.mergeState;
  const gitConv = readiness?.gitConvergence ?? null;
  const mergeStatus = mergeState
    ? mergeStateLabel(mergeState)
    : snapshot.leaseStatus ?? "Unknown";

  const codeEnteredMainWorkspace =
    gitConv?.succeeded === true &&
    Boolean(gitConv.destinationNewHead) &&
    gitConv.destinationNewHead !== gitConv.destinationPreviousHead;

  let lastResult: string | null = null;
  if (codeEnteredMainWorkspace) {
    lastResult = `Code entered main workspace (${gitConv!.strategy}, commit ${shortCommit(gitConv!.destinationNewHead)}).`;
  } else if (mergeState === "merged" || snapshot.leaseStatus === "Merged") {
    lastResult = codeEnteredMainWorkspace
      ? "Accepted: lease Merged, task Complete."
      : "Lease Merged in metadata — verify git.convergence.succeeded evidence; code may not be in main workspace.";
  } else if (mergeState === "revoked" || snapshot.leaseStatus === "Revoked") {
    lastResult =
      "Revoked: worktree and mirror paths are kept for inspection (not deleted by JoyZoning).";
  } else if (mergeState === "merge_conflict" || mergeState === "merge_failed") {
    lastResult = conflict?.reason ?? "Resolve conflicts before accepting.";
  }

  return {
    mainWorkspacePath: main,
    workerWorktreePath,
    workerMirrorPath,
    workerBranch: readiness?.mergeTargetBranch?.trim() ?? null,
    headCommit: readiness?.headCommit ?? null,
    baseCommit: readiness?.baseCommit ?? null,
    mergeStatus,
    mergeStatusDetail: conflict?.reason ?? readiness?.verificationSummary ?? null,
    conflictFiles: conflict?.conflictFiles ?? [],
    destinationWorkspacePath: readiness?.mergeTargetWorkspaceRoot?.trim() || main,
    acceptOperation: codeEnteredMainWorkspace
      ? "Git convergence completed on last accept."
      : ACCEPT_RESULT_PENDING_SUMMARY,
    readyToMergeGate:
      "POST /api/tasks/{id}/verification (passing) → lease ReadyForReview → merge queue ready_to_merge",
    lastResult,
    changedFilesSummary: readiness?.changedFilesSummary ?? [],
    changedFilesCount: readiness?.changedFilesCount ?? 0,
    codeEnteredMainWorkspace,
    destinationNewHead: gitConv?.destinationNewHead ?? null,
    convergenceStrategy: gitConv?.strategy ?? null,
    appliedFiles: gitConv?.appliedFiles ?? [],
  };
}

export function formatCommitLine(head: string | null, base: string | null): string {
  if (!head && !base) return "—";
  if (head && base) return `${shortCommit(head)} (base ${shortCommit(base)})`;
  return shortCommit(head ?? base);
}
