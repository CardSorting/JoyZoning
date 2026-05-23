import { api } from "@/lib/api";
import type { ChatAction } from "@/lib/chat-types";

export async function runChatAction(
  actionId: string,
  ctx: {
    sessionId: string;
    taskId?: string;
    workspaceRoot?: string;
    onNavigate?: (path: string) => void;
    onNotify?: (message: string, role?: "system" | "tool" | "error") => void;
  },
): Promise<void> {
  const { sessionId, taskId, workspaceRoot, onNavigate, onNotify } = ctx;

  switch (actionId) {
    case "accept":
      if (!taskId) return;
      await api.approveMerge(taskId);
      onNotify?.("Accepted worker result.", "tool");
      break;
    case "revoke":
      if (!taskId) return;
      await api.revokeLease(taskId, "Revoked from chat");
      onNotify?.("Worker lease revoked.", "tool");
      break;
    case "open-workspace":
      if (workspaceRoot) {
        await api.openPath(workspaceRoot);
        onNotify?.("Opened workspace in Finder.", "tool");
      } else if (taskId) {
        onNavigate?.(`/console?taskId=${encodeURIComponent(taskId)}`);
      }
      break;
    case "view-diff":
      if (taskId) {
        onNavigate?.(`/console?taskId=${encodeURIComponent(taskId)}&panel=diff`);
      }
      break;
    case "open-worker":
      if (taskId) {
        onNavigate?.(`/console?taskId=${encodeURIComponent(taskId)}`);
      }
      break;
    case "open-console":
      onNavigate?.(
        taskId
          ? `/console?taskId=${encodeURIComponent(taskId)}`
          : `/console?sessionId=${encodeURIComponent(sessionId)}`,
      );
      break;
    case "run-reconcile": {
      const report = await api.reconcileAuthority(sessionId);
      onNotify?.(
        `Reconciliation: ${report.autoAccepted} auto-accepted, ${report.blocked} blocked.`,
        "tool",
      );
      break;
    }
    case "evidence":
      if (taskId) {
        onNavigate?.(`/console?taskId=${encodeURIComponent(taskId)}&panel=evidence`);
      }
      break;
    case "start-task":
      if (taskId) {
        await api.dispatchTask(taskId);
        onNotify?.("Task dispatch started.", "tool");
      }
      break;
    case "stop-task":
      if (taskId) {
        await api.revokeLease(taskId, "Stopped from chat");
        onNotify?.("Task stopped.", "tool");
      }
      break;
    default:
      onNotify?.(`Action "${actionId}" is not available.`, "error");
  }
}

export function defaultActionsForCard(
  actionIds: string[],
): ChatAction[] {
  const labels: Record<string, ChatAction> = {
    accept: { id: "accept", label: "Accept result", variant: "primary" },
    revoke: { id: "revoke", label: "Revoke", variant: "danger" },
    "open-workspace": { id: "open-workspace", label: "Open workspace" },
    "view-diff": { id: "view-diff", label: "View diff" },
    "open-worker": { id: "open-worker", label: "Open worker" },
    "open-console": { id: "open-console", label: "Open operator console" },
    "run-reconcile": { id: "run-reconcile", label: "Run reconciliation", variant: "primary" },
    evidence: { id: "evidence", label: "Show evidence" },
    "start-task": { id: "start-task", label: "Start task", variant: "primary" },
    "stop-task": { id: "stop-task", label: "Stop task", variant: "danger" },
  };
  return actionIds.map((id) => labels[id]).filter(Boolean);
}
