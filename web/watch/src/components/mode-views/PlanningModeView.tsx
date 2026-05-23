"use client";

import type { BoardTask } from "@/lib/kanban";
import { KanbanBoard } from "../KanbanBoard";
import { ModeHandoffLink } from "../ModeHandoffLink";
import type { JoyZoningOperationalMode } from "@/lib/operational-modes";
import { OPERATIONAL_MODES } from "@/lib/operational-modes";

export function PlanningModeView({
  boardTasks,
  activeTaskId,
  leaseStatus,
  onSelectTask,
  onNavigateMode,
  theme = "pet",
}: {
  boardTasks: BoardTask[];
  activeTaskId: string;
  leaseStatus?: string | null;
  onSelectTask?: (id: string) => void;
  onNavigateMode: (mode: JoyZoningOperationalMode) => void;
  theme?: "pet" | "campfire";
}) {
  const titleCls = theme === "pet" ? "text-pet-cream" : "text-campfire-text";
  const mutedCls = theme === "pet" ? "text-pet-muted" : "text-campfire-muted";
  const hasActiveRun =
    leaseStatus === "Running" ||
    leaseStatus === "Leased" ||
    leaseStatus === "Verifying" ||
    leaseStatus === "ReadyForReview" ||
    leaseStatus === "Blocked";

  return (
    <div data-joyzoning-mode="planning" data-canonical-surface="true" className="space-y-4">
      <header>
        <h2 className={`text-sm font-bold ${titleCls}`}>{OPERATIONAL_MODES.planning.title}</h2>
        <p className={`text-xs ${mutedCls}`}>{OPERATIONAL_MODES.planning.primaryQuestion}</p>
        <p className={`mt-1 text-[11px] ${mutedCls} opacity-80`}>
          Kanban intent only — no merge or revoke here.
        </p>
      </header>

      <KanbanBoard
        tasks={boardTasks}
        activeTaskId={activeTaskId}
        onSelectTask={onSelectTask}
      />

      {hasActiveRun && (
        <ModeHandoffLink
          targetMode="execution"
          label="View active execution"
          reason="See live worker, mirror, and lease health for the selected card"
          onNavigate={onNavigateMode}
        />
      )}

      {leaseStatus === "ReadyForReview" && (
        <ModeHandoffLink
          targetMode="review"
          label="Review worker output"
          reason="Verification and merge decisions live in Review mode"
          onNavigate={onNavigateMode}
        />
      )}
    </div>
  );
}
