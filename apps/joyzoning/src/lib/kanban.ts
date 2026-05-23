import type { JourneyStep, LiveTaskSnapshot } from "./types";

export interface BoardTask {
  id: string;
  title: string;
  status: number;
}

export interface KanbanColumn {
  id: string;
  label: string;
  short: string;
  statuses: number[];
}

/** JoyZoning task statuses grouped for the watch board. */
export const KANBAN_COLUMNS: KanbanColumn[] = [
  { id: "queue", label: "Queue", short: "Q", statuses: [0, 1] },
  { id: "build", label: "Building", short: "B", statuses: [2] },
  { id: "check", label: "Checks", short: "C", statuses: [3, 4] },
  { id: "blocked", label: "Blocked", short: "!", statuses: [5] },
  { id: "done", label: "Done", short: "✓", statuses: [6] },
];

export const TASK_STATUS_LABEL: Record<number, string> = {
  0: "Backlog",
  1: "Ready",
  2: "Running",
  3: "Review",
  4: "Verifying",
  5: "Blocked",
  6: "Done",
};

export function columnForTaskStatus(status: number): KanbanColumn {
  return (
    KANBAN_COLUMNS.find((c) => c.statuses.includes(status)) ?? KANBAN_COLUMNS[0]
  );
}

export function tasksByColumn(tasks: BoardTask[]): Map<string, BoardTask[]> {
  const map = new Map<string, BoardTask[]>();
  for (const col of KANBAN_COLUMNS) map.set(col.id, []);
  for (const t of tasks) {
    const col = columnForTaskStatus(t.status);
    map.get(col.id)!.push(t);
  }
  return map;
}

export interface PipelineStage {
  id: string;
  label: string;
  icon: string;
  state: JourneyStep["state"];
}

/** Maps lease + journey into a left-to-right execution pipeline. */
export function buildPipelineStages(snapshot: LiveTaskSnapshot): PipelineStage[] {
  const steps = snapshot.display?.steps ?? [];
  const lease = snapshot.leaseStatus ?? "";
  const blocked = Boolean(snapshot.blockedReason) || lease === "Blocked";

  const stageDefs: { id: string; label: string; icon: string; stepId: string }[] = [
    { id: "dispatch", label: "Kanban", icon: "📋", stepId: "start" },
    { id: "lease", label: "Lease", icon: "🔑", stepId: "start" },
    { id: "build", label: "Build", icon: "🏗️", stepId: "build" },
    { id: "verify", label: "Verify", icon: "🔍", stepId: "verify" },
    { id: "review", label: "Review", icon: "👀", stepId: "review" },
  ];

  const stepState = (stepId: string): JourneyStep["state"] => {
    const s = steps.find((x) => x.id === stepId);
    return s?.state ?? "pending";
  };

  let leaseState: JourneyStep["state"] = "pending";
  if (lease === "Leased") leaseState = "current";
  else if (["Running", "Blocked", "Verifying", "ReadyForReview", "Merged"].includes(lease))
    leaseState = "complete";
  if (blocked && lease === "Blocked") leaseState = "failed";

  return stageDefs.map((d, i) => {
    let state: JourneyStep["state"];
    if (d.id === "lease") state = leaseState;
    else if (d.id === "dispatch") {
      state = lease ? "complete" : stepState("start");
    } else state = stepState(d.stepId);

    if (lease === "Merged" && i <= stageDefs.length - 1) state = "complete";

    return { id: d.id, label: d.label, icon: d.icon, state };
  });
}

export function leaseColumnLabel(leaseStatus: string | null): string {
  const id = leaseToKanbanColumnId(leaseStatus);
  return KANBAN_COLUMNS.find((c) => c.id === id)?.label ?? id;
}

export function leaseToKanbanColumnId(leaseStatus: string | null): string {
  switch (leaseStatus) {
    case "Leased":
      return "queue";
    case "Running":
      return "build";
    case "Verifying":
    case "ReadyForReview":
      return "check";
    case "Blocked":
      return "blocked";
    case "Merged":
      return "done";
    default:
      return "queue";
  }
}
