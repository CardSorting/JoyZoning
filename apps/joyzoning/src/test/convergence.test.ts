import { describe, expect, it } from "vitest";
import { buildConvergenceModel, formatCommitLine } from "@/lib/convergence";
import type { MergeWorkerEntry } from "@/lib/merge-queue";
import { minimalShellProps } from "./fixtures";

const readyWorker: MergeWorkerEntry = {
  taskId: "task-1",
  taskTitle: "Card",
  leaseId: "lease-1",
  workspacePath: "/workspace",
  kanbanRevision: 1,
  kanbanPushedRevision: 1,
  kanbanStatus: "NeedsApproval",
  leaseStatus: "ReadyForReview",
  mergeState: "ready_to_merge",
  mergeReadiness: {
    worktreePath: "/workspace",
    mergeTargetWorkspaceRoot: "/workspace",
    mergeTargetBranch: "joyzoning/card-abc12345",
    headCommit: "deadbeefdeadbeefdeadbeefdeadbeefdeadbeef",
    baseCommit: "cafebabecafebabecafebabecafebabecafebabe",
    isDirty: true,
    changedFilesCount: 2,
    changedFilesSummary: ["src/a.ts", "src/b.ts"],
    verificationPassed: true,
  },
  mergeConflict: {
    category: "overlapping_files",
    reason: "Overlap",
    conflictFiles: ["src/shared.ts"],
  },
};

describe("buildConvergenceModel", () => {
  it("maps main workspace, worktree, commits, and conflicts", () => {
    const model = buildConvergenceModel(
      {
        ...minimalShellProps.snapshot,
        sessionWorkspaceRoot: "/workspace",
        worktreePath: "/workspace",
      },
      readyWorker,
      "/workspace",
    );

    expect(model.mainWorkspacePath).toBe("/workspace");
    expect(model.workerWorktreePath).toBe("/workspace");
    expect(model.workerWorkspacePath).toBe("/workspace");
    expect(model.headCommit).toContain("deadbee");
    expect(model.conflictFiles).toContain("src/shared.ts");
  });

  it("formats commit line", () => {
    expect(formatCommitLine("abc123", "def456")).toContain("abc123");
  });
});
