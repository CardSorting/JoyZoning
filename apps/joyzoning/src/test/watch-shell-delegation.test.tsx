import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { WatchDashboard } from "@/components/WatchDashboard";
import { PetShell } from "@/components/pet/PetShell";
import { OperatorModeShell } from "@/components/OperatorModeShell";
import { minimalLiveBinding, minimalShellProps, minimalSnapshot } from "./fixtures";

vi.mock("@/lib/api", () => ({
  api: {
    operationalModes: vi.fn().mockResolvedValue({ modes: [], registryTransitions: [] }),
    mergeQueue: vi.fn().mockResolvedValue({
      sessionId: "session-1",
      sessionWorkspaceRoot: "/workspace",
      readyToMerge: [
        {
          taskId: "task-1",
          taskTitle: "Ready card",
          leaseId: "lease-1",
          workspacePath: "/workspace",
          kanbanRevision: 1,
          kanbanPushedRevision: 1,
          kanbanStatus: "NeedsApproval",
          leaseStatus: "ReadyForReview",
          mergeState: "ready_to_merge",
          mergeReadiness: {
            mergeTargetWorkspaceRoot: "/workspace",
            isDirty: false,
            changedFilesCount: 0,
            changedFilesSummary: [],
          },
        },
      ],
      mergeConflicts: [],
      completedWorkers: [],
      revokedAbandoned: [],
      allWorkers: [],
      warnings: [],
    }),
    parallelWorkers: vi.fn().mockResolvedValue({
      sessionId: "session-1",
      sessionWorkspaceRoot: "/workspace",
      parallelActive: false,
      protocol: "jsdp",
      warnings: [],
      workers: [],
    }),
    openPath: vi.fn(),
  },
}));

const componentsDir = join(__dirname, "..", "components");

function readComponentSource(name: string) {
  return readFileSync(join(componentsDir, name), "utf8");
}

describe("legacy Watch wrappers", () => {
  it("WatchDashboard delegates to OperatorModeShell", () => {
    const src = readComponentSource("WatchDashboard.tsx");
    expect(src).toContain("OperatorModeShell");
    expect(src).not.toMatch(/MergeQueuePanel/);
  });

  it("PetShell delegates to OperatorModeShell", () => {
    const src = readComponentSource("pet/PetShell.tsx");
    expect(src).toContain("OperatorModeShell");
  });

  it("WatchDashboard renders operator console with live binding", () => {
    render(
      <WatchDashboard state="live" binding={minimalLiveBinding()} newFilePaths={new Set()} />,
    );
    expect(screen.getByTestId("operator-console")).toBeInTheDocument();
  });
});

describe("OperatorModeShell", () => {
  beforeEach(() => {
    sessionStorage.clear();
    window.history.replaceState({}, "", "/");
  });

  it("exposes approve on single screen without switching mode tabs", async () => {
    render(
      <OperatorModeShell
        {...minimalShellProps}
        snapshot={minimalSnapshot({
          leaseStatus: "ReadyForReview",
          modeNavigation: { recommendedMode: "review", availableTransitions: [] },
        })}
      />,
    );

    expect(screen.getByTestId("operator-console")).toBeInTheDocument();
    expect(await screen.findByRole("button", { name: /^Accept$/i })).toBeInTheDocument();
    expect(screen.queryByRole("tab", { name: /Habitat/i })).not.toBeInTheDocument();
  });
});

describe("PetShell alias", () => {
  it("renders operator console with live binding", () => {
    render(<PetShell state="live" binding={minimalLiveBinding()} />);
    expect(screen.getByTestId("operator-console")).toBeInTheDocument();
  });

  it("renders disconnected shell without session binding", () => {
    render(<PetShell state="disconnected" reason="No session selected" />);
    expect(screen.getByTestId("watch-operator-disconnected")).toBeInTheDocument();
  });
});
