import type { LiveTaskSnapshot } from "@/lib/types";
import { createWatchLiveBinding, type WatchLiveBinding } from "@/lib/watch-live-binding";

export function emptyDisplay(): LiveTaskSnapshot["display"] {
  return {
    headline: "Test run",
    subheadline: "",
    activityState: "waiting",
    progressPercent: 0,
    currentStepIndex: 0,
    stepCount: 1,
    stepProgressLabel: "",
    phaseLabel: "Planning",
    navigationSummary: "",
    currentStepTitle: null,
    timeGuidance: null,
    staleWarning: null,
    pollMode: "normal",
    steps: [],
    deliverables: [],
    nextActions: [],
    recentActivity: [],
    helpTips: [],
  };
}

export function minimalSnapshot(
  overrides: Partial<LiveTaskSnapshot> = {},
): LiveTaskSnapshot {
  return {
    taskId: "task-1",
    title: "Test task",
    leaseStatus: "Running",
    blockedReason: null,
    worktreePath: null,
    sessionWorkspaceRoot: "/workspace",
    recommendedPollSeconds: 5,
    display: emptyDisplay(),
    ...overrides,
  };
}

export const minimalShellPropsInput = {
  sessionId: "session-1",
  boardTasks: [{ id: "task-1", title: "Test task", status: 1 }],
  events: [],
  stream: [],
  files: [],
  pulseTicks: 0,
  connLabel: "Live · test",
  resting: false,
  onRestingChange: () => {},
  onNudge: () => {},
  onDepart: () => {},
};

export function minimalLiveBinding(
  snapshotOverrides: Partial<LiveTaskSnapshot> = {},
): WatchLiveBinding {
  return createWatchLiveBinding({
    ...minimalShellPropsInput,
    snapshot: minimalSnapshot(snapshotOverrides),
  });
}

/** Unbranded props for OperatorModeShell tests that bypass WatchDashboard guard. */
export const minimalShellProps = {
  ...minimalShellPropsInput,
  snapshot: minimalSnapshot(),
};
