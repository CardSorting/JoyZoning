export type StepState = "pending" | "current" | "complete" | "failed";

export interface JourneyStep {
  id: string;
  label: string;
  state: StepState;
}

export interface Deliverable {
  id: string;
  label: string;
  done: boolean;
  count: number | null;
}

export interface LiveDisplay {
  headline: string;
  subheadline: string;
  activityState: string;
  progressPercent: number;
  currentStepIndex: number;
  stepCount: number;
  stepProgressLabel: string;
  phaseLabel: string;
  navigationSummary: string;
  currentStepTitle: string | null;
  timeGuidance: string | null;
  staleWarning: string | null;
  pollMode: string;
  steps: JourneyStep[];
  deliverables: Deliverable[];
  nextActions: string[];
  recentActivity: string[];
  helpTips: string[];
}

export interface LiveProgress {
  appScreens: number;
  featureFiles: number;
  sharedFiles: number;
  hasPackageJson: boolean;
  hasReadme: boolean;
  filesCopiedThisTick: number;
  worktreeFileCount: number;
}

export interface LiveTaskSnapshot {
  taskId: string;
  title: string;
  message?: string;
  leaseStatus: string | null;
  blockedReason: string | null;
  worktreePath: string | null;
  sessionWorkspaceRoot: string | null;
  liveMirrorRoot?: string | null;
  mirrorMode?: string | null;
  mirrorKeyId?: string | null;
  leaseId?: string | null;
  isSharedSessionRootMirror?: boolean;
  liveIndexJson?: string | null;
  recommendedPollSeconds: number;
  progress?: LiveProgress;
  display: LiveDisplay;
  recentEvidence?: unknown[];
  updatedAt?: string;
  modeNavigation?: {
    recommendedMode: string;
    availableTransitions: {
      targetMode: string;
      label: string;
      reason: string;
      handoffKind?: string | null;
    }[];
  };
}

export interface WatchSession {
  id: string;
  name: string;
  workspaceRoot: string;
  workspaceKey?: string;
  hermesProfile: string | null;
}

export interface ActiveTaskSummary {
  taskId: string;
  sessionId: string;
  title: string;
  leaseStatus: string;
  blockedReason: string | null;
  worktreePath: string;
}

export interface WatchBootstrap {
  watchUrl: string;
  sessions: WatchSession[];
  activeTasks: ActiveTaskSummary[];
}

export interface JoyEvent {
  id?: number;
  Id?: number;
  summary?: string;
  Summary?: string;
  type?: string;
  Type?: string;
  createdAt?: string;
  CreatedAt?: string;
}

export interface StreamLine {
  id: string;
  at: string;
  text: string;
  kind: "file" | "tool" | "sync";
}
