import { apiUrl } from "./config";
import type { JoyEvent, LiveTaskSnapshot, WatchBootstrap } from "./types";
import type { ParallelWorkersSnapshot } from "./parallel-workers";
import type { MergeQueueSnapshot } from "./merge-queue";
import type { DecisionPreflightSnapshot, OperatorDecisionAction } from "./operator-decision";
import type { JoyZoningOperationalMode } from "./operational-modes";

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
  operationalModes: () =>
    fetchJson<{
      modes: {
        slug: JoyZoningOperationalMode;
        title: string;
        primaryQuestion: string;
        shortDescription: string;
        isCanonicalOperationalSurface: boolean;
        forbiddenInMode: string[];
        allowsKanbanMutation: boolean;
        allowsMergeApproveRevoke: boolean;
      }[];
      registryTransitions: { targetMode: string; label: string; reason: string }[];
    }>("/api/operational-modes"),
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
  parallelWorkers: (sessionId: string) =>
    fetchJson<ParallelWorkersSnapshot>(
      `/api/sessions/${encodeURIComponent(sessionId)}/parallel-workers`,
    ),
  mergeQueue: (sessionId: string) =>
    fetchJson<MergeQueueSnapshot>(
      `/api/sessions/${encodeURIComponent(sessionId)}/merge-queue`,
    ),
  decisionPreflight: (
    sessionId: string,
    executionSessionId: string,
    action: OperatorDecisionAction,
  ) =>
    fetchJson<DecisionPreflightSnapshot>(
      `/api/sessions/${encodeURIComponent(sessionId)}/workers/${encodeURIComponent(executionSessionId)}/decision-preflight?action=${encodeURIComponent(action)}`,
    ),
  approveMerge: (taskId: string) =>
    fetchJson<unknown>(`/api/tasks/${encodeURIComponent(taskId)}/lease/merge`, {
      method: "POST",
    }),
  revokeLease: (taskId: string, reason?: string) =>
    fetchJson<unknown>(`/api/tasks/${encodeURIComponent(taskId)}/lease/revoke`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ reason: reason ?? null }),
    }),
  openPath: (path: string) =>
    fetchJson<{ ok: boolean }>("/api/sessions/open-path", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ path }),
    }),
  events: (taskId: string, since?: number) => {
    const q = since ? `&since=${since}` : "";
    return fetchJson<JoyEvent[]>(
      `/api/events?correlationId=${encodeURIComponent(taskId)}${q}`,
    );
  },
};
