import type { JoyZoningOperationalMode } from "./operational-modes";

/** Planning: kanban intent only — no merge controls. */
export function allowsKanbanMutation(mode: JoyZoningOperationalMode) {
  return mode === "planning";
}

/** Review: approve/revoke and merge queue only. */
export function allowsMergeApproveRevoke(mode: JoyZoningOperationalMode) {
  return mode === "review";
}

/** Habitat never performs authoritative workspace actions. */
export function allowsAuthoritativeActions(mode: JoyZoningOperationalMode) {
  return mode !== "habitat";
}

/** Execution may inspect paths but must route merge decisions to Review. */
export function executionInspectOnly(mode: JoyZoningOperationalMode) {
  return mode === "execution";
}
