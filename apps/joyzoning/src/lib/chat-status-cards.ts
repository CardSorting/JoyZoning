import {
  authorityBlockMessage,
  collectAutopilotActivity,
  workerNeedsHumanReview,
} from "@/lib/authority";
import type { ChatAction, ChatMessage, StatusCardKind } from "@/lib/chat-types";
import type { MergeWorkerEntry } from "@/lib/merge-queue";
import type { ParallelWorkerEntry } from "@/lib/parallel-workers";

function workerWorkspacePath(worker: ParallelWorkerEntry | MergeWorkerEntry): string | null | undefined {
  return worker.workspacePath ?? worker.mergeReadiness?.worktreePath ?? null;
}

function workerActions(taskId: string, workspacePath?: string | null): ChatAction[] {
  const actions: ChatAction[] = [
    { id: "accept", label: "Accept result", variant: "primary" },
    { id: "revoke", label: "Revoke", variant: "danger" },
    { id: "evidence", label: "Show evidence" },
  ];
  if (workspacePath) {
    actions.splice(1, 0, { id: "open-workspace", label: "Open workspace" });
    actions.splice(2, 0, { id: "view-diff", label: "View diff" });
  }
  return actions;
}

function cardFromWorker(
  kind: StatusCardKind,
  worker: ParallelWorkerEntry | MergeWorkerEntry,
  content: string,
  actions?: ChatAction[],
): Omit<ChatMessage, "id" | "role"> {
  return {
    statusCard: kind,
    title: worker.taskTitle,
    taskId: worker.taskId,
    taskTitle: worker.taskTitle,
    content,
    timestamp: new Date().toISOString(),
    actions: actions ?? workerActions(worker.taskId, workerWorkspacePath(worker)),
  };
}

export function cardsFromWorkers(
  workers: (ParallelWorkerEntry | MergeWorkerEntry)[],
): Omit<ChatMessage, "id" | "role">[] {
  const cards: Omit<ChatMessage, "id" | "role">[] = [];
  const activity = collectAutopilotActivity(workers);

  for (const a of activity.accepted) {
    cards.push({
      statusCard: "autopilot_accepted",
      title: a.taskTitle,
      taskId: a.taskId,
      taskTitle: a.taskTitle,
      content: `Autopilot accepted "${a.taskTitle}" without manual review.`,
      timestamp: a.at ?? new Date().toISOString(),
      actions: [{ id: "evidence", label: "Show evidence" }],
    });
  }

  for (const w of workers) {
    const auth = w.authority;
    if (auth?.reasonCodes?.includes("protected_path")) {
      cards.push(
        cardFromWorker(
          "protected_path_block",
          w,
          authorityBlockMessage(w) ?? "Blocked: touched protected path.",
        ),
      );
      continue;
    }

    if (
      w.mergeConflict?.category === "overlapping_files" ||
      auth?.reasonCodes?.includes("overlapping_ready_worker")
    ) {
      cards.push(
        cardFromWorker(
          "overlap_block",
          w,
          authorityBlockMessage(w) ?? "Blocked: overlapping worker diff.",
        ),
      );
      continue;
    }

    const conv = w.mergeReadiness?.gitConvergence;
    if (conv?.succeeded === true) {
      cards.push(
        cardFromWorker(
          "git_convergence_succeeded",
          w,
          `Git convergence succeeded (${conv.strategy}).`,
          [{ id: "view-diff", label: "View diff" }],
        ),
      );
    } else if (conv?.succeeded === false) {
      cards.push(
        cardFromWorker(
          "git_convergence_failed",
          w,
          conv.errorMessage ?? "Git convergence failed.",
          [{ id: "run-reconcile", label: "Run reconciliation", variant: "primary" }],
        ),
      );
    }

    const verification = w.mergeReadiness?.verificationPassed;
    if (verification === true) {
      cards.push(
        cardFromWorker(
          "verification_passed",
          w,
          w.mergeReadiness?.verificationSummary ?? "Verification passed.",
        ),
      );
    } else if (verification === false) {
      cards.push(
        cardFromWorker(
          "verification_failed",
          w,
          w.mergeReadiness?.verificationSummary ?? "Verification failed.",
        ),
      );
    }

    if (workerNeedsHumanReview(w) && !auth?.reasonCodes?.includes("protected_path")) {
      const overlap = w.mergeConflict?.category === "overlapping_files";
      if (!overlap) {
        cards.push(
          cardFromWorker(
            "needs_review",
            w,
            authorityBlockMessage(w) ?? "Worker is ready but needs human review.",
          ),
        );
      }
    }
  }

  for (const b of activity.blocked) {
    if (cards.some((c) => c.taskId === b.taskId)) continue;
    cards.push({
      statusCard: "needs_review",
      title: b.taskTitle,
      taskId: b.taskId,
      taskTitle: b.taskTitle,
      content: b.message,
      timestamp: new Date().toISOString(),
      actions: workerActions(b.taskId),
    });
  }

  return cards;
}

export function cardFromJoyEvent(
  type: string,
  summary: string,
  correlationId?: string,
): Omit<ChatMessage, "id" | "role"> | null {
  const base = {
    timestamp: new Date().toISOString(),
    taskId: correlationId,
  };

  switch (type) {
    case "task.created":
      return {
        ...base,
        statusCard: "task_started",
        title: "Task started",
        content: summary || "A new task was created.",
        actions: correlationId
          ? [{ id: "open-worker", label: "Open worker" }]
          : undefined,
      };
    case "dietcode.execution.started":
      return {
        ...base,
        statusCard: "worker_dispatched",
        title: "Worker dispatched",
        content: summary || "DietCode worker dispatched.",
        actions: correlationId
          ? [{ id: "open-worker", label: "Open worker" }]
          : undefined,
      };
    case "verification.report.attached":
      return {
        ...base,
        statusCard: summary.toLowerCase().includes("fail")
          ? "verification_failed"
          : "verification_passed",
        title: summary.toLowerCase().includes("fail")
          ? "Verification failed"
          : "Verification passed",
        content: summary,
        actions: correlationId ? [{ id: "evidence", label: "Show evidence" }] : undefined,
      };
    case "hermes.approval.requested":
      return {
        ...base,
        statusCard: "needs_review",
        title: "Approval requested",
        content: summary || "Hermes requested operator approval.",
        actions: [{ id: "open-console", label: "Open operator console" }],
      };
    default:
      return null;
  }
}

export function cardFingerprint(msg: Pick<ChatMessage, "statusCard" | "taskId" | "content">) {
  return `${msg.statusCard ?? "msg"}:${msg.taskId ?? ""}:${msg.content.slice(0, 80)}`;
}
