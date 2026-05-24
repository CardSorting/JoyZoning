import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { OperatorModeShell } from "@/components/OperatorModeShell";
import { minimalShellProps } from "./fixtures";

function buildReadyWorker() {
  return {
    taskId: "task-1",
    taskTitle: "Ready card",
    executionSessionId: "exec-1",
    leaseId: "lease-1",
    hermesSessionId: "h1",
    workspacePath: "/workspace",
    kanbanRevision: 1,
    kanbanPushedRevision: 1,
    kanbanStatus: "NeedsApproval",
    leaseStatus: "ReadyForReview",
    mergeState: "ready_to_merge",
    mergeReadiness: {
      worktreePath: "/workspace",
      mergeTargetWorkspaceRoot: "/workspace",
      isDirty: false,
      changedFilesCount: 1,
      changedFilesSummary: ["src/a.ts"],
      verificationPassed: true,
      testsRun: true,
      verificationSummary: "1/1 commands passed",
    },
    decisionSummary: {
      taskId: "task-1",
      taskTitle: "Ready card",
      executionSessionId: "exec-1",
      leaseId: "lease-1",
      mergeState: "ready_to_merge",
      changedFilesCount: 1,
      changedFilesSummary: ["src/a.ts"],
      verificationStatus: "passed",
      conflictStatus: "none",
      worktreePath: "/workspace",
      workspacePath: "/workspace",
      riskFlags: {
        hasConflicts: false,
        verificationFailed: false,
        verificationMissing: false,
        dirtyWorktree: false,
        overlapsWithOtherReadyWorker: false,
        largeChangeSet: false,
        staleWorker: false,
        unknownHeadCommit: false,
        active: [],
      },
    },
    approveGuardrails: {
      action: "accept",
      blocked: false,
      blockReasons: [],
      warnings: [],
      requiresAcknowledgement: false,
    },
    revokeGuardrails: {
      action: "revoke",
      blocked: false,
      blockReasons: [],
      warnings: [],
      requiresAcknowledgement: false,
    },
    authority: {
      profile: "BalancedAuto",
      riskLevel: "medium",
      autoAcceptAllowed: false,
      needsHumanReview: true,
      reasonCodes: ["protected_path"],
      humanMessages: ["Blocked: touched protected path src/auth/login.ts"],
      wasAutoAccepted: false,
    },
  };
}

vi.mock("@/lib/api", () => {
  const readyWorker = buildReadyWorker();
  return {
    api: {
      operationalModes: vi.fn().mockResolvedValue({ modes: [], registryTransitions: [] }),
      mergeQueue: vi.fn().mockResolvedValue({
        sessionId: "session-1",
        sessionWorkspaceRoot: "/workspace",
        readyToMerge: [readyWorker],
        mergeConflicts: [],
        completedWorkers: [],
        revokedAbandoned: [],
        allWorkers: [readyWorker],
        warnings: [],
      }),
      parallelWorkers: vi.fn().mockResolvedValue({
        sessionId: "session-1",
        sessionWorkspaceRoot: "/workspace",
        parallelActive: false,
        protocol: "jsdp",
        warnings: [],
        workers: [readyWorker],
      }),
      openPath: vi.fn().mockResolvedValue({ ok: true }),
      decisionPreflight: vi.fn().mockResolvedValue({
        action: "accept",
        decisionSummary: readyWorker.decisionSummary,
        guardrails: readyWorker.approveGuardrails,
      }),
      approveMerge: vi.fn().mockResolvedValue({ ok: true }),
    },
  };
});

describe("operator console single screen", () => {
  beforeEach(() => {
    sessionStorage.clear();
    window.history.replaceState({}, "", "/");
  });

  it("shows accept result without mode tab navigation for ready-for-review", async () => {
    render(<OperatorModeShell {...minimalShellProps} />);
    expect((await screen.findAllByText(/Ready card/i)).length).toBeGreaterThan(0);
    expect(screen.getByRole("button", { name: /accept result/i })).toBeInTheDocument();
  });

  it("opens confirm dialog and calls approve on accept", async () => {
    const user = userEvent.setup();
    render(<OperatorModeShell {...minimalShellProps} />);
    await screen.findAllByText(/Ready card/i);
    await user.click(screen.getByRole("button", { name: /accept result/i }));
    const dialog = await screen.findByRole("dialog");
    await user.click(within(dialog).getByRole("button", { name: /accept result/i }));
    expect(await screen.findByText(/merge submitted|accepted/i)).toBeTruthy();
  });
});
