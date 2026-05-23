import { describe, expect, it } from "vitest";
import { isAcceptResultAction } from "@/lib/operator-decision";

describe("operator decision actions", () => {
  it("treats accept and legacy approve as accept-result", () => {
    expect(isAcceptResultAction("accept")).toBe(true);
    expect(isAcceptResultAction("approve")).toBe(true);
    expect(isAcceptResultAction("revoke")).toBe(false);
    expect(isAcceptResultAction("inspect")).toBe(false);
  });
});
