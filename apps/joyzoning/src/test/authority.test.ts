import { describe, expect, it } from "vitest";
import {
  authorityBlockMessage,
  collectAutopilotActivity,
  workerNeedsHumanReview,
} from "@/lib/authority";
import type { MergeWorkerEntry } from "@/lib/merge-queue";

function worker(partial: Partial<MergeWorkerEntry> & { taskId: string }): MergeWorkerEntry {
  return {
    taskId: partial.taskId,
    taskTitle: partial.taskTitle ?? "Task",
    executionSessionId: "exec",
    leaseId: "lease",
    hermesSessionId: "h",
    workspacePath: "/workspace",
    kanbanRevision: 1,
    kanbanPushedRevision: 1,
    kanbanStatus: "NeedsApproval",
    leaseStatus: partial.leaseStatus ?? "ReadyForReview",
    mergeState: partial.mergeState ?? "ready_to_merge",
    mergeReadiness: partial.mergeReadiness,
    decisionSummary: partial.decisionSummary,
    approveGuardrails: partial.approveGuardrails,
    revokeGuardrails: partial.revokeGuardrails,
    authority: partial.authority,
    mergeConflict: partial.mergeConflict,
  } as MergeWorkerEntry;
}

describe("authority", () => {
  it("treats policy-blocked ready workers as needs review", () => {
    const w = worker({
      taskId: "t1",
      authority: {
        profile: "BalancedAuto",
        riskLevel: "high",
        autoAcceptAllowed: false,
        needsHumanReview: true,
        reasonCodes: ["protected_path"],
        humanMessages: ["Blocked: touched protected path src/auth/login.ts"],
        wasAutoAccepted: false,
      },
    });
    expect(workerNeedsHumanReview(w)).toBe(true);
    expect(authorityBlockMessage(w)).toContain("protected path");
  });

  it("excludes auto-accepted workers from needs review", () => {
    const w = worker({
      taskId: "t2",
      leaseStatus: "Merged",
      mergeState: "merged",
      authority: {
        profile: "BalancedAuto",
        riskLevel: "low",
        autoAcceptAllowed: true,
        needsHumanReview: false,
        reasonCodes: [],
        humanMessages: [],
        wasAutoAccepted: true,
        autoAcceptedAt: "2026-05-23T12:00:00Z",
      },
    });
    expect(workerNeedsHumanReview(w)).toBe(false);
  });

  it("formats overlap block message from merge conflict files", () => {
    const w = worker({
      taskId: "o",
      mergeState: "merge_conflict",
      mergeConflict: {
        category: "overlapping_files",
        reason: "Changed files overlap another ready worker.",
        conflictFiles: ["src/shared.cs"],
      },
    });
    expect(authorityBlockMessage(w)).toContain("src/shared.cs");
  });

  it("collects autopilot activity buckets", () => {
    const accepted = worker({
      taskId: "a",
      authority: {
        profile: "BalancedAuto",
        riskLevel: "low",
        autoAcceptAllowed: true,
        needsHumanReview: false,
        reasonCodes: [],
        humanMessages: [],
        wasAutoAccepted: true,
      },
    });
    const blocked = worker({
      taskId: "b",
      authority: {
        profile: "BalancedAuto",
        riskLevel: "high",
        autoAcceptAllowed: false,
        needsHumanReview: true,
        reasonCodes: ["verification_failed"],
        humanMessages: ["Blocked: verification failed"],
        wasAutoAccepted: false,
      },
    });
    const activity = collectAutopilotActivity([accepted, blocked]);
    expect(activity.accepted).toHaveLength(1);
    expect(activity.blocked).toHaveLength(1);
    expect(activity.blocked[0]?.message).toContain("verification failed");
  });
});
