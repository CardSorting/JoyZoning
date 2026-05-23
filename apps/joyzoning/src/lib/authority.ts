import type { MergeWorkerEntry } from "@/lib/merge-queue";
import type { ParallelWorkerEntry, ParallelWorkersSnapshot } from "@/lib/parallel-workers";

export interface SessionAuthority {
  profile: string;
  profileLabel: string;
  autopilotEnabled: boolean;
}

export interface WorkerAuthority {
  profile: string;
  riskLevel: string;
  autoAcceptAllowed: boolean;
  needsHumanReview: boolean;
  reasonCodes: string[];
  humanMessages: string[];
  autoAcceptedAt?: string | null;
  wasAutoAccepted: boolean;
}

export function sessionAuthorityFromSnapshot(
  workers: ParallelWorkersSnapshot | null,
): SessionAuthority {
  const a = workers?.authority;
  return {
    profile: a?.profile ?? "BalancedAuto",
    profileLabel: a?.profileLabel ?? "Balanced-Auto (bounded YOLO)",
    autopilotEnabled: a?.autopilotEnabled ?? true,
  };
}

export function workerNeedsHumanReview(worker: ParallelWorkerEntry | MergeWorkerEntry): boolean {
  if (worker.authority?.wasAutoAccepted) return false;
  if (worker.leaseStatus === "Merged" || worker.mergeState === "merged") return false;
  if (worker.authority?.needsHumanReview === true) return true;
  if (worker.mergeState === "ready_to_merge" && worker.leaseStatus === "ReadyForReview") {
    if (!worker.authority) return true;
    return worker.authority.autoAcceptAllowed === false;
  }
  return worker.mergeState === "merge_conflict" || worker.mergeState === "merge_failed";
}

export function authorityBlockMessage(worker: ParallelWorkerEntry | MergeWorkerEntry): string | null {
  const auth = worker.authority;
  if (auth?.humanMessages?.length) return auth.humanMessages[0] ?? null;
  if (worker.mergeConflict?.category === "overlapping_files") {
    const files = worker.mergeConflict.conflictFiles?.slice(0, 5).join(", ");
    return files
      ? `Blocked: overlapping worker diff (${files})`
      : "Blocked: overlapping worker diff";
  }
  if (worker.mergeConflict?.reason) return `Blocked: ${worker.mergeConflict.reason}`;
  if (auth?.reasonCodes?.includes("merge_observability_unknown")) {
    return "Blocked: merge conflict risk unknown (could not resolve changed files)";
  }
  return null;
}

export function collectAutopilotActivity(workers: (ParallelWorkerEntry | MergeWorkerEntry)[]) {
  const accepted: { taskTitle: string; taskId: string; at?: string | null }[] = [];
  const blocked: { taskTitle: string; taskId: string; message: string }[] = [];

  for (const w of workers) {
    if (w.authority?.wasAutoAccepted) {
      accepted.push({
        taskTitle: w.taskTitle,
        taskId: w.taskId,
        at: w.authority.autoAcceptedAt,
      });
    } else if (workerNeedsHumanReview(w) && w.authority) {
      blocked.push({
        taskTitle: w.taskTitle,
        taskId: w.taskId,
        message: authorityBlockMessage(w) ?? "Needs human review",
      });
    }
  }

  return { accepted, blocked };
}
