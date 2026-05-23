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
  liveMirrorPath: "/workspace/mirror",
  liveMarkdownPath: null,
  healthState: "active" as const,
  lifecycleStatus: "active",
  lastMirroredAt: new Date().toISOString(),
  kanbanRevision: 1,
  kanbanPushedRevision: 1,
  kanbanStatus: "NeedsApproval",
  leaseStatus: "ReadyForReview",
  worktreePath: "/workspace/wt",
  isSharedSessionRootMirror: false,
  mergeState: "ready_to_merge",
  mergeReadiness: {
    worktreePath: "/workspace/wt",
    liveMirrorPath: "/workspace/mirror",
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
    worktreePath: "/workspace/wt",
    liveMirrorPath: "/workspace/mirror",
    riskFlags: {
      hasConflicts: false,
      verificationFailed: false,
      verificationMissing: false,
      dirtyWorktree: false,
      overlapsWithOtherReadyWorker: false,
      largeChangeSet: false,
      staleWorker: false,
      mirrorMissing: false,
      unknownHeadCommit: false,
      active: [],
    },
  },
  approveGuardrails: { blocked: false, blockReasons: [], warnings: [], requiresAcknowledgement: false },
  revokeGuardrails: { blocked: false, blockReasons: [], warnings: [], requiresAcknowledgement: false },
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
      liveMirrorMode: "PerExecution",
      disableSharedSessionRootMirrorWhenParallel: true,
      sharedSessionRootMirroringSuppressed: false,
      sessionRootIsCanonicalLiveState: true,
      canonicalLiveStateHint: "",
      warnings: [],
      workers: [readyWorker],
    }),
    openPath: vi.fn().mockResolvedValue({ ok: true }),
    decisionPreflight: vi.fn().mockResolvedValue({
      action: "accept",
      summary: readyWorker.decisionSummary,
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
    render(
      <OperatorModeShell
        {...minimalShellProps}
        snapshot={{
          ...minimalShellProps.snapshot,
          leaseStatus: "ReadyForReview",
          modeNavigation: {
            recommendedMode: "review",
            availableTransitions: [
              {
                targetMode: "execution",
                label: "Watch execution",
                reason: "Workers",
              },
            ],
          },
        }}
      />,
    );

    expect(screen.getByTestId("operator-console")).toBeInTheDocument();
    expect(screen.queryByRole("tab", { name: /Habitat/i })).not.toBeInTheDocument();

    const accept = await screen.findByRole("button", { name: /^Accept$/i });
    expect(accept).toBeInTheDocument();

    const emphasis = screen.getByTestId("mode-emphasis-bar");
    expect(within(emphasis).getByText(/recommended/i)).toBeInTheDocument();

    expect(screen.getByTestId("primary-action")).toHaveTextContent(/Accept result/i);
    expect(screen.getByTestId("authority-autopilot-bar")).toBeInTheDocument();
    expect(screen.getByText(/Balanced-Auto/i)).toBeInTheDocument();
    expect(screen.getByTestId("merge-queue-panel")).toBeInTheDocument();
    expect(within(screen.getByTestId("merge-queue-panel")).getByText(/Needs review/i)).toBeInTheDocument();
    expect(screen.getByTestId("worker-list")).toBeInTheDocument();
    expect(screen.getByTestId("convergence-panel")).toBeInTheDocument();
    expect(screen.getByText(/Main workspace \(canonical\)/i)).toBeInTheDocument();
    expect(screen.getByText(/\/workspace\/wt/)).toBeInTheDocument();
    expect(screen.getByText(/converges them into the main workspace/i)).toBeInTheDocument();
    expect(screen.getByTestId("open-main-workspace")).toBeInTheDocument();
    expect(screen.getByTestId("open-worker-workspace")).toBeInTheDocument();
  });

  it("does not hide review actions when emphasis is planning", async () => {
    render(
      <OperatorModeShell
        {...minimalShellProps}
        snapshot={{
          ...minimalShellProps.snapshot,
          leaseStatus: "ReadyForReview",
          modeNavigation: { recommendedMode: "review", availableTransitions: [] },
        }}
      />,
    );

    await screen.findByRole("button", { name: /^Accept$/i });

    const emphasis = screen.getByTestId("mode-emphasis-bar");
    await userEvent.click(within(emphasis).getByRole("tab", { name: /Planning/i }));

    expect(screen.getAllByRole("button", { name: /^Accept$/i }).length).toBeGreaterThan(0);
    expect(screen.getByTestId("merge-queue-panel")).toBeInTheDocument();
  });

  it("uses Open workspace label not Open folder", async () => {
    render(
      <OperatorModeShell
        {...minimalShellProps}
        snapshot={{
          ...minimalShellProps.snapshot,
          leaseStatus: "ReadyForReview",
          modeNavigation: { recommendedMode: "review", availableTransitions: [] },
        }}
      />,
    );

    expect(await screen.findByRole("button", { name: /Open workspace/i })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Open folder/i })).not.toBeInTheDocument();
  });
});
