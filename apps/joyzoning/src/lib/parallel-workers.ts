export interface WorkerWarning {
  code: string;
  message: string;
  leaseId?: string | null;
  taskId?: string | null;
}

export interface ParallelWorkerEntry {
  taskId: string;
  taskTitle: string;
  executionSessionId?: string | null;
  leaseId: string;
  hermesSessionId?: string | null;
  workspacePath?: string | null;
  kanbanRevision: number;
  kanbanPushedRevision: number;
  kanbanStatus: string;
  leaseStatus: string;
  mergeState?: string;
  authorityProfile?: string;
  authority?: {
    profile: string;
    riskLevel: string;
    autoAcceptAllowed: boolean;
    needsHumanReview: boolean;
    reasonCodes: string[];
    humanMessages: string[];
    autoAcceptedAt?: string | null;
    wasAutoAccepted: boolean;
  } | null;
  mergeReadiness?: {
    worktreePath?: string | null;
    mergeTargetWorkspaceRoot?: string;
    mergeTargetBranch?: string | null;
    headCommit?: string | null;
    baseCommit?: string | null;
    isDirty?: boolean;
    changedFilesCount?: number;
    changedFilesSummary?: string[];
    verificationPassed?: boolean | null;
    testsRun?: boolean | null;
    verificationSummary?: string | null;
    gitConvergence?: {
      succeeded: boolean;
      strategy: string;
      destinationPreviousHead?: string | null;
      destinationNewHead?: string | null;
      appliedFiles?: string[];
      errorMessage?: string | null;
    } | null;
  } | null;
  mergeConflict?: {
    category: string;
    reason: string;
    conflictFiles: string[];
  } | null;
  recommendedMode?: string;
  availableModeTransitions?: ModeTransitionHint[];
}

export interface ModeTransitionHint {
  targetMode: string;
  label: string;
  reason: string;
  handoffKind?: string | null;
}

export interface ParallelWorkersSnapshot {
  sessionId: string;
  sessionWorkspaceRoot: string;
  updatedAt?: string;
  protocol?: string;
  authority?: {
    profile: string;
    profileLabel: string;
    autopilotEnabled: boolean;
  };
  parallelActive: boolean;
  warnings: WorkerWarning[];
  workers: ParallelWorkerEntry[];
}
