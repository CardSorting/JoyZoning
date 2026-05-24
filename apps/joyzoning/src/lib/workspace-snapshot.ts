import type { LiveTaskSnapshot } from "./types";
import { leaseLabel } from "./presentation";

/** Builds a watch UI snapshot from canonical workspace polling (no live mirror API). */
export function buildWorkspaceTaskSnapshot(
  taskId: string,
  opts: {
    title?: string;
    leaseStatus?: string | null;
    worktreePath?: string | null;
    sessionWorkspaceRoot?: string | null;
    fileCount?: number;
    filesCopiedThisTick?: number;
    idleMessage?: string;
  },
): LiveTaskSnapshot {
  const fileCount = opts.fileCount ?? 0;
  const leaseStatus = opts.leaseStatus ?? null;
  const active = Boolean(leaseStatus && leaseStatus !== "Merged" && leaseStatus !== "Revoked");

  return {
    taskId,
    title: opts.title ?? taskId,
    message: opts.idleMessage,
    leaseStatus,
    blockedReason: null,
    worktreePath: opts.worktreePath ?? opts.sessionWorkspaceRoot ?? null,
    sessionWorkspaceRoot: opts.sessionWorkspaceRoot ?? opts.worktreePath ?? null,
    recommendedPollSeconds: active ? 3 : 8,
    progress: {
      appScreens: 0,
      featureFiles: fileCount,
      sharedFiles: 0,
      hasPackageJson: false,
      hasReadme: false,
      filesCopiedThisTick: opts.filesCopiedThisTick ?? 0,
      worktreeFileCount: fileCount,
    },
    display: {
      headline: leaseStatus ? leaseLabel(leaseStatus) : "No active lease",
      subheadline: active
        ? `${fileCount} changed file(s) in workspace`
        : (opts.idleMessage ?? "Dispatch a role to start JSDP delivery."),
      activityState: active ? "active" : "waiting",
      progressPercent: active ? Math.min(95, 10 + fileCount * 3) : 0,
      currentStepIndex: 0,
      stepCount: 1,
      stepProgressLabel: active ? "Working in canonical workspace" : "",
      phaseLabel: active ? "Execution" : "Idle",
      navigationSummary: "",
      currentStepTitle: null,
      timeGuidance: null,
      staleWarning: null,
      pollMode: active ? "normal" : "slow",
      steps: [],
      deliverables: [],
      nextActions: [],
      recentActivity: [],
      helpTips: [],
    },
  };
}
