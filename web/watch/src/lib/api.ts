import { apiUrl } from "./config";
import type { JoyEvent, LiveTaskSnapshot, WatchBootstrap } from "./types";

async function fetchJson<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(apiUrl(path), {
    credentials: "include",
    ...init,
    headers: {
      Accept: "application/json",
      ...(init?.headers ?? {}),
    },
  });
  const body = await res.json().catch(() => null);
  if (!res.ok) {
    const msg =
      (body as { message?: string })?.message ??
      (body as { error?: string })?.error ??
      res.statusText;
    throw new Error(msg || `HTTP ${res.status}`);
  }
  return body as T;
}

export const api = {
  health: () => fetchJson<{ status: string }>("/api/health"),
  bootstrap: () => fetchJson<WatchBootstrap>("/api/watch/bootstrap"),
  live: (taskId: string) =>
    fetchJson<LiveTaskSnapshot>(`/api/tasks/${taskId}/live`),
  refreshLive: (taskId: string) =>
    fetchJson<LiveTaskSnapshot>(`/api/tasks/${taskId}/live/refresh`, {
      method: "POST",
    }),
  tasks: (sessionId: string) =>
    fetchJson<{ id: string; title: string; status: number }[]>(
      `/api/tasks?sessionId=${encodeURIComponent(sessionId)}`,
    ),
  changedFiles: (taskId: string) =>
    fetchJson<{ files: (string | { path?: string; Path?: string })[] }>(
      `/api/tasks/${taskId}/workspace/changed`,
    ),
  events: (taskId: string, since?: number) => {
    const q = since ? `&since=${since}` : "";
    return fetchJson<JoyEvent[]>(
      `/api/events?correlationId=${encodeURIComponent(taskId)}${q}`,
    );
  },
};
