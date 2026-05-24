import type { ParallelWorkerEntry } from "./parallel-workers";
import type {
  OperatorActionGuardrails,
  OperatorDecisionSummary,
} from "./operator-decision";

export type WorkerMergeState =
  | "running"
  | "ready_to_merge"
  | "merging"
  | "merged"
  | "merge_conflict"
  | "merge_failed"
  | "revoked"
  | "abandoned"
  | "stale";

export interface GitConvergenceReadiness {
  succeeded: boolean;
  strategy: string;
  destinationPreviousHead?: string | null;
  destinationNewHead?: string | null;
  appliedFiles: string[];
  errorMessage?: string | null;
}

export interface MergeReadiness {
  executionSessionId?: string | null;
  worktreePath?: string | null;
  mergeTargetWorkspaceRoot: string;
  mergeTargetBranch?: string | null;
  headCommit?: string | null;
  baseCommit?: string | null;
  isDirty: boolean;
  changedFilesCount: number;
  changedFilesSummary: string[];
  verificationPassed?: boolean | null;
  testsRun?: boolean | null;
  verificationSummary?: string | null;
  gitConvergence?: GitConvergenceReadiness | null;
}

export interface MergeConflict {
  category: string;
  reason: string;
  conflictFiles: string[];
}

export interface MergeWorkerEntry extends ParallelWorkerEntry {
  mergeState: WorkerMergeState;
  authorityProfile?: string;
  mergeReadiness?: MergeReadiness | null;
  mergeConflict?: MergeConflict | null;
  decisionSummary?: OperatorDecisionSummary | null;
  approveGuardrails?: OperatorActionGuardrails | null;
  revokeGuardrails?: OperatorActionGuardrails | null;
}

export interface MergeQueueSnapshot {
  sessionId: string;
  sessionWorkspaceRoot: string;
  updatedAt?: string;
  readyToMerge: MergeWorkerEntry[];
  mergeConflicts: MergeWorkerEntry[];
  completedWorkers: MergeWorkerEntry[];
  revokedAbandoned: MergeWorkerEntry[];
  allWorkers: MergeWorkerEntry[];
  warnings: { code: string; message: string; leaseId?: string | null; taskId?: string | null }[];
}

export function mergeStateLabel(state: WorkerMergeState): string {
  switch (state) {
    case "ready_to_merge":
      return "Ready to merge";
    case "merging":
      return "Merging";
    case "merged":
      return "Merged";
    case "merge_conflict":
      return "Conflict";
    case "merge_failed":
      return "Merge failed";
    case "revoked":
      return "Revoked";
    case "abandoned":
      return "Abandoned";
    case "stale":
      return "Stale";
    default:
      return "Running";
  }
}

export function mergeStateTone(state: WorkerMergeState): string {
  switch (state) {
    case "ready_to_merge":
      return "text-emerald-300 border-emerald-500/40 bg-emerald-500/10";
    case "merged":
      return "text-sky-300 border-sky-500/40 bg-sky-500/10";
    case "merge_conflict":
    case "merge_failed":
      return "text-red-300 border-red-500/40 bg-red-500/10";
    case "revoked":
    case "abandoned":
      return "text-orange-300 border-orange-500/40 bg-orange-500/10";
    case "stale":
      return "text-amber-300 border-amber-500/40 bg-amber-500/10";
    default:
      return "text-zinc-400 border-zinc-500/40 bg-zinc-500/10";
  }
}

export function shortCommit(hash?: string | null) {
  if (!hash) return "—";
  return hash.length > 10 ? hash.slice(0, 7) : hash;
}
