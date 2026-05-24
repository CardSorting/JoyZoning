import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { WatchDashboard } from "@/components/WatchDashboard";
import { minimalLiveBinding } from "./fixtures";

vi.mock("@/lib/api", () => ({
  api: {
    operationalModes: vi.fn().mockResolvedValue({ modes: [], registryTransitions: [] }),
    mergeQueue: vi.fn().mockResolvedValue({
      sessionId: "s",
      sessionWorkspaceRoot: "/w",
      readyToMerge: [],
      mergeConflicts: [],
      completedWorkers: [],
      revokedAbandoned: [],
      allWorkers: [],
      warnings: [],
    }),
    parallelWorkers: vi.fn().mockResolvedValue({
      sessionId: "s",
      sessionWorkspaceRoot: "/w",
      parallelActive: false,
      protocol: "jsdp",
      warnings: [],
      workers: [],
    }),
    openPath: vi.fn(),
  },
}));

describe("WatchDashboard misuse guard", () => {
  beforeEach(() => {
    window.history.replaceState({}, "", "/?mode=habitat");
    sessionStorage.clear();
  });

  it("renders disconnected shell instead of operator chrome when state is disconnected", () => {
    render(
      <WatchDashboard
        state="disconnected"
        reason="Connection label is missing"
        onRetry={() => {}}
      />,
    );
    expect(screen.getByTestId("watch-operator-disconnected")).toBeInTheDocument();
    expect(screen.queryByTestId("operator-mode-shell")).not.toBeInTheDocument();
  });

  it("renders preview shell for preview state", () => {
    render(
      <WatchDashboard state="preview" message="Embedder must pass WatchLiveBinding" />,
    );
    expect(screen.getByTestId("watch-operator-disconnected")).toBeInTheDocument();
    expect(screen.queryByTestId("operator-mode-shell")).not.toBeInTheDocument();
  });

  it("renders operator shell only with branded live binding", () => {
    render(
      <WatchDashboard state="live" binding={minimalLiveBinding()} newFilePaths={new Set()} />,
    );
    expect(screen.getByTestId("operator-mode-shell")).toBeInTheDocument();
    expect(screen.queryByTestId("watch-operator-disconnected")).not.toBeInTheDocument();
  });

  it("rejects unbranded binding object at runtime", () => {
    const fake = { ...minimalLiveBinding(), __watchLiveBinding: false };
    render(
      <WatchDashboard state="live" binding={fake as unknown as ReturnType<typeof minimalLiveBinding>} />,
    );
    expect(screen.getByTestId("watch-operator-disconnected")).toBeInTheDocument();
    expect(screen.queryByTestId("operator-mode-shell")).not.toBeInTheDocument();
  });
});
