import { describe, expect, it } from "vitest";
import {
  createWatchLiveBinding,
  inferModeNavigationFromSnapshot,
  resolveModeNavigation,
  tryCreateWatchLiveBinding,
} from "@/lib/watch-live-binding";
import { minimalLiveBinding, minimalShellPropsInput, minimalSnapshot } from "./fixtures";

describe("watch live binding", () => {
  it("rejects empty connection label (placeholder live state)", () => {
    const result = tryCreateWatchLiveBinding({
      ...minimalShellPropsInput,
      connLabel: "",
      snapshot: minimalSnapshot(),
    });
    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error).toMatch(/connection label/i);
    }
  });

  it("rejects error connection state", () => {
    const result = tryCreateWatchLiveBinding({
      ...minimalShellPropsInput,
      connLabel: "Error",
      snapshot: minimalSnapshot(),
    });
    expect(result.ok).toBe(false);
  });

  it("rejects missing session id", () => {
    const result = tryCreateWatchLiveBinding({
      ...minimalShellPropsInput,
      sessionId: "",
      snapshot: minimalSnapshot(),
    });
    expect(result.ok).toBe(false);
  });

  it("brands valid runtime binding", () => {
    const binding = minimalLiveBinding();
    expect(binding.__watchLiveBinding).toBe(true);
    expect(createWatchLiveBinding).toBeDefined();
  });

  it("resolveModeNavigation uses server hints when well-formed", () => {
    const nav = resolveModeNavigation(
      minimalSnapshot({
        modeNavigation: {
          recommendedMode: "review",
          availableTransitions: [
            {
              targetMode: "execution",
              label: "Inspect worker",
              reason: "Mirrors and leases",
            },
          ],
        },
      }),
    );
    expect(nav.inferred).toBe(false);
    expect(nav.recommendedMode).toBe("review");
    expect(nav.availableTransitions).toHaveLength(1);
  });

  it("resolveModeNavigation infers when backend payload is malformed", () => {
    const nav = resolveModeNavigation(
      minimalSnapshot({
        leaseStatus: "ReadyForReview",
        modeNavigation: {
          recommendedMode: "not-a-mode",
          availableTransitions: [],
        },
      }),
    );
    expect(nav.inferred).toBe(true);
    expect(nav.recommendedMode).toBe("review");
  });

  it("inferModeNavigationFromSnapshot maps lease status", () => {
    expect(
      inferModeNavigationFromSnapshot(minimalSnapshot({ leaseStatus: "Running" }))
        .recommendedMode,
    ).toBe("execution");
  });
});
