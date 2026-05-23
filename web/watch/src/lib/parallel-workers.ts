export type MirrorHealthState =
  | "active"
  | "stale"
  | "completed"
  | "skipped_collision"
  | "skipped_parallel_shared_root_guard"
  | "failed_copy"
  | "pruned"
  | "unknown";

export interface MirrorWarning {
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
  liveMirrorPath?: string | null;
  liveMarkdownPath?: string | null;
  healthState: MirrorHealthState;
  lifecycleStatus: string;
  lastMirroredAt?: string | null;
  kanbanRevision: number;
  kanbanPushedRevision: number;
  kanbanStatus: string;
  leaseStatus: string;
  worktreePath?: string | null;
  isSharedSessionRootMirror: boolean;
  registryCollision?: {
    occupyingLeaseId: string;
    occupyingTaskId?: string | null;
    occupyingMirrorRoot?: string | null;
  } | null;
  mergeState?: string;
  mergeReadiness?: {
    worktreePath?: string | null;
    liveMirrorPath?: string | null;
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
  } | null;
  mergeConflict?: {
    category: string;
    reason: string;
    conflictFiles: string[];
  } | null;
  recommendedMode?: string;
  availableModeTransitions?: {
    targetMode: string;
    label: string;
    reason: string;
    handoffKind?: string | null;
  }[];
}

export interface ParallelWorkersSnapshot {
  sessionId: string;
  sessionWorkspaceRoot: string;
  updatedAt?: string;
  parallelActive: boolean;
  liveMirrorMode: string;
  disableSharedSessionRootMirrorWhenParallel: boolean;
  sharedSessionRootMirroringSuppressed: boolean;
  sessionRootIsCanonicalLiveState: boolean;
  canonicalLiveStateHint: string;
  indexJsonPath?: string | null;
  warnings: MirrorWarning[];
  workers: ParallelWorkerEntry[];
}

export function healthLabel(state: MirrorHealthState): string {
  switch (state) {
    case "active":
      return "Active";
    case "stale":
      return "Stale";
    case "completed":
      return "Completed";
    case "skipped_collision":
      return "Skipped (collision)";
    case "skipped_parallel_shared_root_guard":
      return "Skipped (parallel guard)";
    case "failed_copy":
      return "Copy failed";
    case "pruned":
      return "Pruned";
    default:
      return "Unknown";
  }
}

export function healthTone(state: MirrorHealthState): string {
  switch (state) {
    case "active":
      return "text-emerald-400 border-emerald-500/40 bg-emerald-500/10";
    case "stale":
      return "text-amber-300 border-amber-500/40 bg-amber-500/10";
    case "completed":
      return "text-sky-300 border-sky-500/40 bg-sky-500/10";
    case "skipped_collision":
    case "skipped_parallel_shared_root_guard":
      return "text-orange-300 border-orange-500/40 bg-orange-500/10";
    case "failed_copy":
      return "text-red-300 border-red-500/40 bg-red-500/10";
    case "pruned":
      return "text-zinc-400 border-zinc-500/40 bg-zinc-500/10";
    default:
      return "text-zinc-400 border-zinc-500/40 bg-zinc-500/10";
  }
}
