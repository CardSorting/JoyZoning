import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { HabitatAuthorityChecklistPanel } from "@/components/HabitatAuthorityChecklistPanel";

const fetchHabitatAuthorityChecklist = vi.fn();

vi.mock("@/lib/habitat-authority-checklist", () => ({
  fetchHabitatAuthorityChecklist: () => fetchHabitatAuthorityChecklist(),
}));

describe("HabitatAuthorityChecklistPanel", () => {
  beforeEach(() => {
    fetchHabitatAuthorityChecklist.mockReset();
  });

  it("renders authority checklist items including observe-only habitat role", async () => {
    fetchHabitatAuthorityChecklist.mockResolvedValue({
      allPassed: true,
      runtimeOwner: "hermes",
      habitatRole: "observe-only",
      items: [
        {
          id: "hermes_runtime_owner",
          label: "Hermes runtime owner detected",
          ok: true,
          detail: "Hermes API healthy",
        },
        {
          id: "habitat_observe_only",
          label: "JoyZoning habitat role: observe-only",
          ok: true,
          detail: "JoyZoning supervises",
        },
        {
          id: "legacy_shim_inactive",
          label: "apps/agent-runtime marked LegacyRuntimeShim, not active authority",
          ok: true,
          detail: "External Hermes is canonical",
        },
      ],
    });

    render(<HabitatAuthorityChecklistPanel />);

    await waitFor(() => {
      expect(screen.getByTestId("habitat-authority-checklist")).toBeInTheDocument();
    });
    expect(screen.getByText(/JoyZoning habitat role: observe-only/i)).toBeInTheDocument();
    expect(screen.getByText(/LegacyRuntimeShim/i)).toBeInTheDocument();
  });
});
