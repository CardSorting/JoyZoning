import { api } from "./api";

export type PathActionResult = { ok: true } | { ok: false; error: string };

export async function copyPath(path: string): Promise<PathActionResult> {
  if (!path?.trim()) {
    return { ok: false, error: "No path to copy" };
  }
  try {
    await navigator.clipboard.writeText(path);
    return { ok: true };
  } catch {
    return { ok: false, error: "Clipboard unavailable" };
  }
}

export async function openPathInShell(path: string): Promise<PathActionResult> {
  if (!path?.trim()) {
    return { ok: false, error: "No path to open" };
  }
  try {
    const res = await api.openPath(path);
    if (res?.ok === false) {
      return { ok: false, error: "Desktop shell could not open path" };
    }
    return { ok: true };
  } catch (e) {
    return {
      ok: false,
      error: e instanceof Error ? e.message : "Failed to open path",
    };
  }
}
