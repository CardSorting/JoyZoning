import { describe, expect, it } from "vitest";
import { buildConvergenceModel, formatCommitLine } from "@/lib/convergence";
import type { MergeWorkerEntry } from "@/lib/merge-queue";
import { minimalShellProps } from "./fixtures";

const readyWorker: MergeWorkerEntry = {
  taskId: "task-1",
  taskTitle: "Card",
  leaseId: "lease-1",
  healthState: "active",
  lifecycleStatus: "active",
  kanbanRevision: 1,
  kanbanPushedRevision: 1,
  kanbanStatus: "NeedsApproval",
  leaseStatus: "ReadyForReview",
  worktreePath: "/workspace/.joyzoning/worktrees/abc12345",
  isSharedSessionRootMirror: false,
  mergeState: "ready_to_merge",
  mergeReadiness: {
    worktreePath: "/workspace/.joyzoning/worktrees/abc12345",
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
        worktreePath: "/workspace/.joyzoning/worktrees/abc12345",
      },
      readyWorker,
      "/workspace",
    );

    expect(model.mainWorkspacePath).toBe("/workspace");
    expect(model.workerWorktreePath).toBe("/workspace/.joyzoning/worktrees/abc12345");
    expect(model.workerBranch).toBe("joyzoning/card-abc12345");
    expect(model.conflictFiles).toEqual(["src/shared.ts"]);
    expect(model.changedFilesCount).toBe(2);
    expect(model.acceptOperation).toMatch(/git/i);
  });

  it("shows code entered main when git convergence succeeded", () => {
    const model = buildConvergenceModel(
      minimalShellProps.snapshot,
      {
        ...readyWorker,
        mergeState: "merged",
        mergeReadiness: {
          ...readyWorker.mergeReadiness!,
          gitConvergence: {
            succeeded: true,
            strategy: "squash_branch",
            destinationPreviousHead: "aaaa",
            destinationNewHead: "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            appliedFiles: ["src/a.ts"],
          },
        },
      },
      "/workspace",
    );
    expect(model.codeEnteredMainWorkspace).toBe(true);
    expect(model.lastResult).toMatch(/Code entered main workspace/i);
  });
});

describe("formatCommitLine", () => {
  it("shortens hashes", () => {
    expect(formatCommitLine("deadbeefdeadbeefdeadbeefdeadbeefdeadbeef", "cafebabe")).toMatch(
      /^deadbee/,
    );
  });
});
