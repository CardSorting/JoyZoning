import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { ConvergencePanel } from "@/components/ConvergencePanel";
import { HERMES_CONVERGENCE_NOTE } from "@/lib/operator-labels";
import { minimalShellProps } from "./fixtures";
import type { MergeWorkerEntry } from "@/lib/merge-queue";

const fetchHermesConvergence = vi.fn();

vi.mock("@/lib/hermes-convergence", () => ({
  fetchHermesConvergence: (...args: unknown[]) => fetchHermesConvergence(...args),
  formatHermesConvergenceState: (state?: string) =>
    state ? state.replace(/_/g, " ") : "No Hermes observations yet",
}));

const readyWorker: MergeWorkerEntry = {
  taskId: "task-abc",
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
    mergeTargetBranch: "joyzoning/card-abc",
    headCommit: "deadbeef",
    baseCommit: "cafebabe",
    isDirty: true,
    changedFilesCount: 1,
    changedFilesSummary: ["src/a.ts"],
    verificationPassed: true,
  },
  mergeConflict: null,
};

describe("Hermes observation E2E (Watch ConvergencePanel)", () => {
  beforeEach(() => {
    fetchHermesConvergence.mockReset();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it("renders observe-only banner and ready_for_review state from Hermes mirror", async () => {
    fetchHermesConvergence.mockResolvedValue({
      scopeId: "task-abc",
      observed: true,
      authoritative: false,
      state: "ready_for_review",
      lastEventType: "convergence.ready_for_review",
      note: HERMES_CONVERGENCE_NOTE,
    });

    render(
      <ConvergencePanel
        snapshot={{ ...minimalShellProps.snapshot, taskId: "task-abc" }}
        worker={readyWorker}
        sessionWorkspaceRoot="/workspace"
      />,
    );

    expect(await screen.findByTestId("hermes-convergence-observe")).toBeInTheDocument();
    expect(screen.getByText(/Hermes runtime \(observe-only\)/i)).toBeInTheDocument();
    expect(screen.getByText(/ready for review/i)).toBeInTheDocument();
    expect(screen.getByText(HERMES_CONVERGENCE_NOTE)).toBeInTheDocument();
    expect(fetchHermesConvergence).toHaveBeenCalledWith("task-abc");
  });

  it("shows placeholder when no Hermes observations ingested", async () => {
    fetchHermesConvergence.mockResolvedValue({
      scopeId: "task-abc",
      observed: false,
      authoritative: false,
      note: HERMES_CONVERGENCE_NOTE,
    });

    render(
      <ConvergencePanel
        snapshot={{ ...minimalShellProps.snapshot, taskId: "task-abc" }}
        worker={readyWorker}
        sessionWorkspaceRoot="/workspace"
      />,
    );

    await waitFor(() => {
      expect(
        screen.getByText(/No Hermes observations ingested yet for this task/i),
      ).toBeInTheDocument();
    });
  });
});
