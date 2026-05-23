import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { WatchDashboard } from "@/components/WatchDashboard";
import { PetShell } from "@/components/pet/PetShell";
import { OperatorModeShell } from "@/components/OperatorModeShell";
import { HabitatModeView } from "@/components/mode-views/HabitatModeView";
import { buildPetState } from "@/lib/pet";
import { computeCareMeters } from "@/lib/vitals";
import { minimalLiveBinding, minimalShellProps, minimalSnapshot } from "./fixtures";

vi.mock("@/lib/api", () => ({
  api: {
    operationalModes: vi.fn().mockResolvedValue({
      modes: [],
      registryTransitions: [],
    }),
    mergeQueue: vi.fn().mockResolvedValue({
      sessionId: "session-1",
      sessionWorkspaceRoot: "/workspace",
      readyToMerge: [
        {
          taskId: "task-1",
          taskTitle: "Ready card",
          leaseId: "lease-1",
          healthState: "active",
          lifecycleStatus: "active",
          kanbanRevision: 1,
          kanbanPushedRevision: 1,
          kanbanStatus: "NeedsApproval",
          leaseStatus: "ReadyForReview",
          isSharedSessionRootMirror: false,
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
      updatedAt: new Date().toISOString(),
      parallelActive: false,
      liveMirrorMode: "PerExecution",
      disableSharedSessionRootMirrorWhenParallel: true,
      sharedSessionRootMirroringSuppressed: false,
      sessionRootIsCanonicalLiveState: true,
      canonicalLiveStateHint: "",
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
  it("WatchDashboard delegates to OperatorModeShell without stacked panels", () => {
    const src = readComponentSource("WatchDashboard.tsx");
    expect(src).toContain("OperatorModeShell");
    expect(src).not.toMatch(/MergeQueuePanel/);
    expect(src).not.toMatch(/KanbanBoard/);
    expect(src).not.toMatch(/ParallelWorkersPanel/);
  });

  it("PetShell delegates to OperatorModeShell without stacked panels", () => {
    const src = readComponentSource("pet/PetShell.tsx");
    expect(src).toContain("OperatorModeShell");
    expect(src).not.toMatch(/MergeQueuePanel/);
    expect(src).not.toMatch(/ParallelWorkersPanel/);
  });

  it("WatchDashboard renders the canonical shell only with live binding", () => {
    render(
      <WatchDashboard state="live" binding={minimalLiveBinding()} newFilePaths={new Set()} />,
    );
    expect(screen.getByTestId("operator-mode-shell")).toBeInTheDocument();
    expect(screen.getByTestId("operator-mode-shell")).toHaveAttribute(
      "data-legacy-wrapper",
      "watch-dashboard",
    );
  });
});

describe("OperatorModeShell mode boundaries", () => {
  beforeEach(() => {
    window.history.replaceState({}, "", "/?mode=planning");
    sessionStorage.clear();
  });

  it("does not expose approve/revoke outside Review mode", async () => {
    window.history.replaceState({}, "", "/?mode=planning");
    render(
      <OperatorModeShell
        {...minimalShellProps}
        snapshot={minimalSnapshot({
          leaseStatus: "Running",
          modeNavigation: { recommendedMode: "execution", availableTransitions: [] },
        })}
      />,
    );

    expect(screen.queryByRole("button", { name: /^approve$/i })).not.toBeInTheDocument();

    const nav = screen.getByRole("navigation", { name: "Operational mode" });
    await userEvent.click(within(nav).getByRole("tab", { name: /Review/i }));

    expect(await screen.findByRole("button", { name: /^approve$/i })).toBeInTheDocument();
  });

  it("marks habitat as non-canonical", () => {
    window.history.replaceState({}, "", "/?mode=habitat");
    const snapshot = minimalSnapshot();
    const meters = computeCareMeters(snapshot, minimalShellProps.boardTasks, 0);
    const pet = buildPetState(snapshot, meters, []);

    render(
      <HabitatModeView
        snapshot={snapshot}
        pet={pet}
        meters={meters}
        thoughts={[]}
        resting={false}
        onRunAction={() => {}}
        onNavigateMode={() => {}}
      />,
    );

    const habitat = document.querySelector('[data-joyzoning-mode="habitat"]');
    expect(habitat).toHaveAttribute("data-canonical-surface", "false");
  });

  it("campfire ambient extras stay non-canonical in habitat", () => {
    window.history.replaceState({}, "", "/?mode=habitat");
    render(
      <OperatorModeShell
        {...minimalShellProps}
        theme="campfire"
        snapshot={minimalSnapshot()}
      />,
    );

    const campfire = screen.getByText(/Campfire glance/i).closest("[data-canonical-surface]");
    expect(campfire).toHaveAttribute("data-canonical-surface", "false");
  });
});

describe("PetShell alias", () => {
  it("renders the canonical shell with live binding", () => {
    render(<PetShell state="live" binding={minimalLiveBinding()} />);
    expect(screen.getByTestId("operator-mode-shell")).toHaveAttribute(
      "data-legacy-wrapper",
      "pet-shell",
    );
  });

  it("renders disconnected shell without session binding", () => {
    render(<PetShell state="disconnected" reason="No session selected" />);
    expect(screen.getByTestId("watch-operator-disconnected")).toBeInTheDocument();
  });
});
