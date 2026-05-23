import { describe, expect, it, vi, beforeEach } from "vitest";
import { copyPath, openPathInShell } from "@/lib/path-actions";

vi.mock("@/lib/api", () => ({
  api: {
    openPath: vi.fn(),
  },
}));

import { api } from "@/lib/api";

describe("path-actions", () => {
  beforeEach(() => {
    vi.mocked(api.openPath).mockReset();
  });

  it("copyPath rejects empty paths", async () => {
    const res = await copyPath("");
    expect(res.ok).toBe(false);
  });

  it("openPathInShell surfaces API failures", async () => {
    vi.mocked(api.openPath).mockRejectedValue(new Error("shell offline"));
    const res = await openPathInShell("/tmp/worktree");
    expect(res.ok).toBe(false);
    if (!res.ok) {
      expect(res.error).toContain("shell offline");
    }
  });

  it("openPathInShell succeeds when API returns ok", async () => {
    vi.mocked(api.openPath).mockResolvedValue({ ok: true });
    const res = await openPathInShell("/tmp/worktree");
    expect(res).toEqual({ ok: true });
  });
});
