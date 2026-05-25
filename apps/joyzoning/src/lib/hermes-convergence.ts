import { apiUrl } from "./config";
import { HERMES_CONVERGENCE_NOTE } from "./operator-labels";

export type HermesConvergenceSnapshot = {
  scopeId: string;
  observed: boolean;
  authoritative: false;
  state?: string;
  lastEventType?: string;
  layer?: string;
  runId?: string | null;
  observedAt?: string;
  note?: string;
  journalHint?: string;
};

export async function fetchHermesConvergence(scopeId: string): Promise<HermesConvergenceSnapshot> {
  const res = await fetch(apiUrl(`/api/hermes/convergence/${encodeURIComponent(scopeId)}`), {
    credentials: "include",
    headers: { Accept: "application/json" },
  });
  const body = (await res.json().catch(() => null)) as HermesConvergenceSnapshot | null;
  if (!res.ok || !body) {
    throw new Error((body as { message?: string } | null)?.message ?? res.statusText);
  }
  return { ...body, note: body.note ?? HERMES_CONVERGENCE_NOTE, authoritative: false };
}

export function formatHermesConvergenceState(state?: string): string {
  if (!state) return "No Hermes observations yet";
  return state.replace(/_/g, " ");
}
