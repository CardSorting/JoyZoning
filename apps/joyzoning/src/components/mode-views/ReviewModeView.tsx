"use client";

import { MergeQueuePanel } from "../MergeQueuePanel";
import { ModeHandoffLink } from "../ModeHandoffLink";
import type { JoyZoningOperationalMode } from "@/lib/operational-modes";
import { OPERATIONAL_MODES } from "@/lib/operational-modes";

export function ReviewModeView({
  sessionId,
  selectedTaskId,
  onSelectTask,
  onNavigateMode,
  theme = "pet",
}: {
  sessionId: string;
  selectedTaskId?: string;
  onSelectTask?: (id: string) => void;
  onNavigateMode: (mode: JoyZoningOperationalMode) => void;
  theme?: "pet" | "campfire";
}) {
  const titleCls = theme === "pet" ? "text-pet-cream" : "text-campfire-text";
  const mutedCls = theme === "pet" ? "text-pet-muted" : "text-campfire-muted";

  return (
    <div data-joyzoning-mode="review" data-canonical-surface="true" className="space-y-4">
      <header>
        <h2 className={`text-sm font-bold ${titleCls}`}>{OPERATIONAL_MODES.review.title}</h2>
        <p className={`text-xs ${mutedCls}`}>{OPERATIONAL_MODES.review.primaryQuestion}</p>
        <p className={`mt-1 text-[11px] ${mutedCls} opacity-80`}>
          GitHub PR-style merge queue — canonical approve/revoke lives here.
        </p>
      </header>

      <MergeQueuePanel
        sessionId={sessionId}
        selectedTaskId={selectedTaskId}
        onSelectTask={onSelectTask}
        authoritativeActions
      />

      <div className="space-y-2">
        <ModeHandoffLink
          targetMode="execution"
          label="View worker"
          reason="Open mirrors, Hermes sessions, and lease health"
          onNavigate={onNavigateMode}
        />
        <ModeHandoffLink
          targetMode="planning"
          label="View on kanban"
          reason="Card intent and backlog context"
          onNavigate={onNavigateMode}
        />
      </div>
    </div>
  );
}
