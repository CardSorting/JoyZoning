import { describe, expect, it } from "vitest";
import {
  allowsAuthoritativeActions,
  allowsKanbanMutation,
  allowsMergeApproveRevoke,
} from "@/lib/mode-guardrails";
import { OPERATIONAL_MODES } from "@/lib/operational-modes";

describe("mode guardrails", () => {
  it("planning allows kanban mutation only", () => {
    expect(allowsKanbanMutation("planning")).toBe(true);
    expect(allowsMergeApproveRevoke("planning")).toBe(false);
  });

  it("review allows merge actions only", () => {
    expect(allowsMergeApproveRevoke("review")).toBe(true);
    expect(allowsKanbanMutation("review")).toBe(false);
  });

  it("habitat is non-canonical and non-authoritative", () => {
    expect(OPERATIONAL_MODES.habitat.isCanonicalOperationalSurface).toBe(false);
    expect(allowsAuthoritativeActions("habitat")).toBe(false);
  });
});
