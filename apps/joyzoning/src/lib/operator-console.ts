import type { MergeWorkerEntry } from "@/lib/merge-queue";
import { workerNeedsHumanReview } from "@/lib/authority";
import { ACCEPT_RESULT_LABEL } from "@/lib/operator-labels";
import type { ParallelWorkerEntry } from "@/lib/parallel-workers";
import type { OperatorDecisionAction } from "@/lib/operator-decision";
import type { JoyZoningOperationalMode } from "@/lib/operational-modes";

export type ConsoleWorker = MergeWorkerEntry | ParallelWorkerEntry;

export function findWorkerForTask(
  taskId: string,
  mergeWorkers: MergeWorkerEntry[],
  parallelWorkers: ParallelWorkerEntry[],
): ConsoleWorker | null {
  const tid = taskId.toLowerCase();
  const fromMerge = mergeWorkers.find((w) => w.taskId.toLowerCase() === tid);
  if (fromMerge) return fromMerge;
  return parallelWorkers.find((w) => w.taskId.toLowerCase() === tid) ?? null;
}

export function workspacePathForWorker(worker: ConsoleWorker | null): string | null {
  if (!worker) return null;
  const merge = worker as MergeWorkerEntry;
  const parallel = worker as ParallelWorkerEntry;
  return (
    parallel.workspacePath ??
    merge.mergeReadiness?.worktreePath ??
    null
  );
}

export function primaryActionForWorker(
  worker: ConsoleWorker | null,
  leaseStatus: string | null,
): {
  label: string;
  action: OperatorDecisionAction | "open_workspace" | "refresh" | null;
  kind: "accept" | "conflict" | "revoke" | "open" | "refresh" | "none";
} {
  if (!worker) {
    if (leaseStatus === "ReadyForReview") {
      return { label: "Loading worker…", action: null, kind: "none" };
    }
    return { label: "Refresh status", action: "refresh", kind: "refresh" };
  }

  const mergeState = "mergeState" in worker ? worker.mergeState : undefined;
  if (mergeState === "ready_to_merge" && workerNeedsHumanReview(worker)) {
    return { label: ACCEPT_RESULT_LABEL, action: "accept", kind: "accept" };
  }
  if (mergeState === "ready_to_merge" && !workerNeedsHumanReview(worker)) {
    return { label: "Autopilot eligible", action: null, kind: "none" };
  }
  if (mergeState === "merge_conflict" || mergeState === "merge_failed") {
    return { label: "Review conflict", action: "inspect", kind: "conflict" };
  }
  if (leaseStatus === "Blocked") {
    return { label: "Retry connection", action: "refresh", kind: "refresh" };
  }
  return { label: "Open workspace", action: "open_workspace", kind: "open" };
}

export function sectionHighlight(
  emphasis: JoyZoningOperationalMode,
  section: JoyZoningOperationalMode,
): string {
  return emphasis === section
    ? "ring-2 ring-sky-500/50 ring-offset-2 ring-offset-zinc-950"
    : "";
}
