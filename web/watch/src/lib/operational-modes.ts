/**
 * JoyZoning operational modes — keep metaphors separate.
 * @see docs/operational-modes.md
 */

export type JoyZoningOperationalMode =
  | "planning"
  | "execution"
  | "review"
  | "habitat";

export const MODE_ORDER: JoyZoningOperationalMode[] = [
  "planning",
  "execution",
  "review",
  "habitat",
];

export interface OperationalModeMeta {
  slug: JoyZoningOperationalMode;
  title: string;
  primaryQuestion: string;
  shortDescription: string;
  mentalModel: string;
  canonicalMetaphor: string;
  isCanonicalOperationalSurface: boolean;
  canonicalSurfaces: string[];
  forbiddenInMode: string[];
  components: string[];
  apiHints: string[];
}

export const OPERATIONAL_MODES: Record<JoyZoningOperationalMode, OperationalModeMeta> = {
  planning: {
    slug: "planning",
    title: "Planning",
    primaryQuestion: "What should happen?",
    shortDescription: "Kanban backlog, cards, and assignment",
    mentalModel: "Backlog, prioritization, assignment",
    canonicalMetaphor: "Jira / Kanban",
    isCanonicalOperationalSurface: true,
    canonicalSurfaces: ["KanbanBoard", "session board", "task picker"],
    forbiddenInMode: ["approve_merge", "revoke_lease"],
    components: ["KanbanBoard", "WelcomePicker", "task picker"],
    apiHints: ["/api/tasks", "/api/tasks/import-kanban"],
  },
  execution: {
    slug: "execution",
    title: "Execution",
    primaryQuestion: "What are workers doing?",
    shortDescription: "Leases, Hermes sessions, mirrors, runtime health",
    mentalModel: "Live workers, leases, mirrors, activity",
    canonicalMetaphor: "Worker orchestration",
    isCanonicalOperationalSurface: true,
    canonicalSurfaces: ["ParallelWorkersPanel", "live snapshot", "Timeline"],
    forbiddenInMode: ["final_merge_approve", "final_merge_revoke"],
    components: [
      "ParallelWorkersPanel",
      "PipelineFlow",
      "ActivityTimeline",
      "PetObservatory",
    ],
    apiHints: ["/api/tasks/{id}/live", "/api/sessions/{id}/parallel-workers"],
  },
  review: {
    slug: "review",
    title: "Review",
    primaryQuestion: "What can safely merge?",
    shortDescription: "Merge queue, verification, conflicts",
    mentalModel: "Merge readiness, verification, conflicts",
    canonicalMetaphor: "GitHub PR review",
    isCanonicalOperationalSurface: true,
    canonicalSurfaces: ["MergeQueuePanel", "WorkerDecisionConfirmDialog"],
    forbiddenInMode: ["kanban_status_mutation"],
    components: ["MergeQueuePanel", "WorkerDecisionConfirmDialog"],
    apiHints: [
      "/api/sessions/{id}/merge-queue",
      "/decision-preflight",
      "/lease/merge",
      "/lease/revoke",
    ],
  },
  habitat: {
    slug: "habitat",
    title: "Habitat",
    primaryQuestion: "What is the workspace atmosphere?",
    shortDescription: "Ambient pet layer — links only, not authoritative",
    mentalModel: "Atmosphere and presence — not authoritative ops",
    canonicalMetaphor: "Ambient observatory",
    isCanonicalOperationalSurface: false,
    canonicalSurfaces: ["SynthesisPet", "CareMeters", "ThoughtBubbles"],
    forbiddenInMode: ["approve_merge", "revoke_lease", "kanban_mutation"],
    components: [
      "SynthesisPet",
      "CareMeters",
      "ThoughtBubbles",
      "HabitatShell",
    ],
    apiHints: ["/api/watch/bootstrap"],
  },
};

export function isOperationalMode(value: string | null | undefined): value is JoyZoningOperationalMode {
  return value === "planning" || value === "execution" || value === "review" || value === "habitat";
}

export function parseModeFromUrl(): JoyZoningOperationalMode | null {
  if (typeof window === "undefined") return null;
  const q = new URLSearchParams(window.location.search).get("mode");
  return isOperationalMode(q) ? q : null;
}

export function inferModeFromApiPath(path: string): JoyZoningOperationalMode | null {
  const p = path.toLowerCase();
  if (
    p.includes("/merge-queue") ||
    p.includes("/decision-preflight") ||
    p.includes("/lease/merge") ||
    p.includes("/lease/revoke")
  ) {
    return "review";
  }
  if (p.includes("/parallel-workers") || p.includes("/live") || p.includes("/dispatch")) {
    return "execution";
  }
  if (p.includes("/tasks") && !p.includes("/live")) {
    return "planning";
  }
  if (p.includes("/watch/")) {
    return "habitat";
  }
  return null;
}
