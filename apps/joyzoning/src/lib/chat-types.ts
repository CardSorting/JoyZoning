export type ChatMessageRole =
  | "user"
  | "assistant"
  | "system"
  | "tool"
  | "error"
  | "escalation";

export type StatusCardKind =
  | "task_started"
  | "worker_dispatched"
  | "verification_passed"
  | "verification_failed"
  | "autopilot_accepted"
  | "needs_review"
  | "git_convergence_succeeded"
  | "git_convergence_failed"
  | "protected_path_block"
  | "overlap_block";

export type HubConnectionState =
  | "connecting"
  | "connected"
  | "reconnecting"
  | "offline";

export interface ChatAction {
  id: string;
  label: string;
  variant?: "primary" | "danger" | "secondary";
}

export interface ChatMessage {
  id: string;
  role: ChatMessageRole;
  content: string;
  streaming?: boolean;
  timestamp?: string;
  statusCard?: StatusCardKind;
  title?: string;
  taskId?: string;
  taskTitle?: string;
  actions?: ChatAction[];
  retryable?: boolean;
}

export interface SessionStatusSummary {
  activeTaskCount: number;
  blockedCount: number;
  needsReviewCount: number;
  autopilotProfile: string;
  autopilotEnabled: boolean;
}

export interface RecentChatThread {
  id: string;
  sessionId: string;
  title: string;
  updatedAt: string;
}

export const COMMAND_CHIPS = [
  {
    id: "explain",
    label: "Explain this workspace",
    prompt: "Explain this workspace — structure, key modules, and how work flows through JoyZoning.",
  },
  {
    id: "yolo",
    label: "Start bounded YOLO",
    prompt:
      "Start a bounded YOLO task: propose one small, verifiable change with clear acceptance criteria.",
  },
  {
    id: "blocked",
    label: "Show blocked workers",
    prompt: "Show blocked workers and why each one needs attention.",
  },
  {
    id: "changes",
    label: "Summarize latest changes",
    prompt: "Summarize the latest workspace changes and what still needs review.",
  },
] as const;

export function statusCardLabel(kind: StatusCardKind): string {
  switch (kind) {
    case "task_started":
      return "Task started";
    case "worker_dispatched":
      return "Worker dispatched";
    case "verification_passed":
      return "Verification passed";
    case "verification_failed":
      return "Verification failed";
    case "autopilot_accepted":
      return "Autopilot accepted";
    case "needs_review":
      return "Needs review";
    case "git_convergence_succeeded":
      return "Git convergence succeeded";
    case "git_convergence_failed":
      return "Git convergence failed";
    case "protected_path_block":
      return "Protected path block";
    case "overlap_block":
      return "Overlap block";
  }
}

export function statusCardTone(kind: StatusCardKind): string {
  switch (kind) {
    case "verification_passed":
    case "autopilot_accepted":
    case "git_convergence_succeeded":
    case "task_started":
    case "worker_dispatched":
      return "border-emerald-500/40 bg-emerald-500/10 text-emerald-200";
    case "verification_failed":
    case "git_convergence_failed":
      return "border-red-500/40 bg-red-500/10 text-red-200";
    case "protected_path_block":
    case "overlap_block":
    case "needs_review":
      return "border-amber-500/40 bg-amber-500/10 text-amber-100";
    default:
      return "border-gpt-border bg-gpt-elevated/60 text-gpt-text";
  }
}
