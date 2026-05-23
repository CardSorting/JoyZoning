"use client";

import type { JoyEvent, LiveTaskSnapshot, StreamLine } from "@/lib/types";
import { PipelineFlow } from "../PipelineFlow";
import { ParallelWorkersPanel } from "../ParallelWorkersPanel";
import { PetObservatory } from "../pet/PetObservatory";
import { ModeHandoffLink } from "../ModeHandoffLink";
import type { JoyZoningOperationalMode } from "@/lib/operational-modes";
import { OPERATIONAL_MODES } from "@/lib/operational-modes";

export function ExecutionModeView({
  sessionId,
  snapshot,
  stream,
  files,
  events,
  connLabel,
  observatoryOpen,
  onObservatoryOpenChange,
  stackHighlight,
  selectedTaskId,
  onSelectTask,
  onNavigateMode,
  theme = "pet",
}: {
  sessionId: string;
  snapshot: LiveTaskSnapshot;
  stream: StreamLine[];
  files: string[];
  events: JoyEvent[];
  connLabel: string;
  observatoryOpen: boolean;
  onObservatoryOpenChange: (open: boolean) => void;
  stackHighlight: boolean;
  selectedTaskId?: string;
  onSelectTask?: (id: string) => void;
  onNavigateMode: (mode: JoyZoningOperationalMode) => void;
  theme?: "pet" | "campfire";
}) {
  const readyForReview = snapshot.leaseStatus === "ReadyForReview";
  const titleCls = theme === "pet" ? "text-pet-cream" : "text-campfire-text";
  const mutedCls = theme === "pet" ? "text-pet-muted" : "text-campfire-muted";

  return (
    <div data-joyzoning-mode="execution" data-canonical-surface="true" className="space-y-4">
      <header>
        <h2 className={`text-sm font-bold ${titleCls}`}>{OPERATIONAL_MODES.execution.title}</h2>
        <p className={`text-xs ${mutedCls}`}>{OPERATIONAL_MODES.execution.primaryQuestion}</p>
        <p className={`mt-1 text-[11px] ${mutedCls} opacity-80`}>
          Inspect runtime here — final merge/revoke happens in Review.
        </p>
      </header>

      <PipelineFlow snapshot={snapshot} />

      <ParallelWorkersPanel
        sessionId={sessionId}
        selectedTaskId={selectedTaskId}
        onSelectTask={onSelectTask}
        theme={theme}
        inspectOnly
        onGoToReview={() => onNavigateMode("review")}
        onNavigateMode={onNavigateMode}
        activeMode="execution"
      />

      {readyForReview && (
        <ModeHandoffLink
          targetMode="review"
          label="Review output for merge"
          reason="Worker is ready for review — approve/revoke in Review mode"
          onNavigate={onNavigateMode}
        />
      )}

      <ModeHandoffLink
        targetMode="planning"
        label="Back to board"
        reason="See kanban intent and sibling cards"
        onNavigate={onNavigateMode}
      />

      <PetObservatory
        snapshot={snapshot}
        events={events}
        stream={stream}
        connLabel={connLabel}
        files={files}
        open={observatoryOpen}
        onOpenChange={onObservatoryOpenChange}
        scrollToStack={stackHighlight}
      />
    </div>
  );
}
