import { apiUrl } from "./config";

export type HabitatAuthorityCheckItem = {
  id: string;
  label: string;
  ok: boolean;
  detail: string;
};

export type HabitatAuthorityChecklist = {
  allPassed: boolean;
  runtimeOwner: string;
  habitatRole: string;
  items: HabitatAuthorityCheckItem[];
};

export async function fetchHabitatAuthorityChecklist(): Promise<HabitatAuthorityChecklist> {
  const res = await fetch(apiUrl("/api/habitat/authority-checklist"), {
    credentials: "include",
    headers: { Accept: "application/json" },
  });
  const body = (await res.json().catch(() => null)) as HabitatAuthorityChecklist | null;
  if (!res.ok || !body) {
    throw new Error((body as { message?: string } | null)?.message ?? res.statusText);
  }
  return body;
}
