import type { WorkerMergeState } from "./merge-queue";

export interface OperatorRiskFlags {
  hasConflicts: boolean;
  verificationFailed: boolean;
  verificationMissing: boolean;
  dirtyWorktree: boolean;
  overlapsWithOtherReadyWorker: boolean;
  largeChangeSet: boolean;
  staleWorker: boolean;
  mirrorMissing: boolean;
  unknownHeadCommit: boolean;
  active: string[];
}

export interface OperatorDecisionSummary {
  taskId: string;
  taskTitle: string;
  executionSessionId?: string | null;
  leaseId: string;
  mergeState: WorkerMergeState;
  changedFilesCount: number;
  changedFilesSummary: string[];
  verificationStatus: string;
  conflictStatus: string;
  baseCommit?: string | null;
  headCommit?: string | null;
  worktreePath?: string | null;
  liveMirrorPath?: string | null;
  riskFlags: OperatorRiskFlags;
}

export interface OperatorActionGuardrails {
  action: string;
  blocked: boolean;
  requiresAcknowledgement: boolean;
  warnings: string[];
  blockReasons: string[];
}

export type OperatorDecisionAction = "approve" | "revoke" | "inspect";

export interface DecisionPreflightSnapshot {
  sessionId: string;
  executionSessionId?: string | null;
  taskId: string;
  action: OperatorDecisionAction;
  decisionSummary: OperatorDecisionSummary;
  guardrails: OperatorActionGuardrails;
}

export const RISK_FLAG_LABELS: Record<string, string> = {
  has_conflicts: "Conflicts",
  verification_failed: "Verification failed",
  verification_missing: "Verification missing",
  dirty_worktree: "Dirty worktree",
  overlaps_with_other_ready_worker: "Overlaps other worker",
  large_change_set: "Large change set",
  stale_worker: "Stale worker",
  mirror_missing: "Mirror missing",
  unknown_head_commit: "Unknown HEAD",
};
