"use client";

/**
 * Canonical Watch operator composition — one active mode at a time with explicit handoffs.
 * WatchApp, WatchDashboard, and PetShell must delegate here (no stacked mode panels).
 * @see docs/operational-modes.md
 */

import { useCallback, useMemo, useState } from "react";
import {
  buildPetState,
  buildThoughtBubbles,
  type PetActionId,
} from "@/lib/pet";
import { computeCareMeters } from "@/lib/vitals";
import { useWatchMode } from "@/hooks/useWatchMode";
import { ModeSwitcher } from "./ModeSwitcher";
import { ModeSuggestedHandoffs } from "./ModeSuggestedHandoffs";
import { PlanningModeView } from "./mode-views/PlanningModeView";
import { ExecutionModeView } from "./mode-views/ExecutionModeView";
import { ReviewModeView } from "./mode-views/ReviewModeView";
import { HabitatModeView } from "./mode-views/HabitatModeView";
import type { JoyZoningOperationalMode } from "@/lib/operational-modes";
import { resolveModeNavigation } from "@/lib/watch-live-binding";
import type { WatchOperatorShellProps } from "./watch-operator-shell-props";

export type OperatorModeShellProps = WatchOperatorShellProps & {
  /** Marks legacy wrappers in tests (watch-dashboard | pet-shell). */
  "data-legacy-wrapper"?: string;
};

export function OperatorModeShell({
  snapshot,
  boardTasks,
  events,
  stream,
  files,
  pulseTicks,
  connLabel,
  resting,
  onRestingChange,
  onNudge,
  onDepart,
  onSelectTask,
  sessionId,
  newFilePaths,
  theme = "pet",
  "data-legacy-wrapper": legacyWrapper,
}: OperatorModeShellProps) {
  const modeNav = resolveModeNavigation(snapshot);
  const recommended = modeNav.recommendedMode;

  const { mode, setMode } = useWatchMode(recommended);
  const [observatoryOpen, setObservatoryOpen] = useState(false);
  const [stackHighlight, setStackHighlight] = useState(false);

  const meters = useMemo(
    () => computeCareMeters(snapshot, boardTasks, pulseTicks),
    [snapshot, boardTasks, pulseTicks],
  );
  const pet = useMemo(
    () => buildPetState(snapshot, meters, events),
    [snapshot, meters, events],
  );
  const thoughts = useMemo(
    () =>
      buildThoughtBubbles(
        events,
        snapshot.display?.recentActivity ?? [],
        pulseTicks,
      ),
    [events, snapshot.display?.recentActivity, pulseTicks],
  );

  const navigateMode = useCallback(
    (target: JoyZoningOperationalMode) => {
      setMode(target);
      if (target === "execution") {
        setObservatoryOpen(true);
      }
    },
    [setMode],
  );

  const runAction = (id: PetActionId) => {
    switch (id) {
      case "feed":
      case "retry":
      case "stabilize":
      case "refresh":
        onNudge();
        break;
      case "rest":
        onRestingChange(!resting);
        break;
      case "clarify":
        navigateMode("execution");
        setObservatoryOpen(true);
        break;
      case "review":
        navigateMode("review");
        break;
      case "stack":
        navigateMode("execution");
        setObservatoryOpen(true);
        setStackHighlight(true);
        setTimeout(() => setStackHighlight(false), 2400);
        break;
    }
  };

  const headerMuted = theme === "pet" ? "text-pet-muted" : "text-campfire-muted";
  const headerText = theme === "pet" ? "text-pet-cream" : "text-campfire-text";
  const leaveBtn =
    theme === "pet"
      ? "shrink-0 rounded-pet pet-card px-3 py-1.5 text-xs text-pet-muted hover:text-pet-cream"
      : "shrink-0 rounded-xl border border-campfire-border px-3 py-1.5 text-xs text-campfire-muted hover:text-campfire-text";

  return (
    <div
      data-testid="operator-mode-shell"
      data-joyzoning-canonical-shell="operator-mode-shell"
      data-legacy-wrapper={legacyWrapper}
      data-watch-theme={theme}
      className="relative min-h-[calc(100vh-7rem)] space-y-4 pb-8"
    >
      <header className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0 flex-1">
          <p className={`text-xs font-semibold uppercase tracking-widest ${headerMuted}`}>
            JoyZone Watch
          </p>
          <p className={`truncate text-sm ${headerText}`}>
            {snapshot.display?.headline ?? snapshot.title}
          </p>
        </div>
        <button type="button" onClick={onDepart} className={leaveBtn}>
          Leave
        </button>
      </header>

      <ModeSwitcher
        active={mode}
        recommendedMode={recommended}
        onChange={setMode}
        theme={theme}
      />

      {recommended !== mode && (
        <ModeSuggestedHandoffs
          recommendedMode={recommended}
          transitions={modeNav.availableTransitions}
          inferred={modeNav.inferred}
          activeMode={mode}
          onNavigate={navigateMode}
          theme={theme}
        />
      )}

      {mode === "planning" && (
        <PlanningModeView
          boardTasks={boardTasks}
          activeTaskId={snapshot.taskId}
          leaseStatus={snapshot.leaseStatus}
          onSelectTask={onSelectTask}
          onNavigateMode={navigateMode}
          theme={theme}
        />
      )}

      {mode === "execution" && (
        <ExecutionModeView
          sessionId={sessionId}
          snapshot={snapshot}
          stream={stream}
          files={files}
          events={events}
          connLabel={connLabel}
          observatoryOpen={observatoryOpen}
          onObservatoryOpenChange={setObservatoryOpen}
          stackHighlight={stackHighlight}
          selectedTaskId={snapshot.taskId}
          onSelectTask={onSelectTask}
          onNavigateMode={navigateMode}
          theme={theme}
        />
      )}

      {mode === "review" && sessionId ? (
        <ReviewModeView
          sessionId={sessionId}
          selectedTaskId={snapshot.taskId}
          onSelectTask={onSelectTask}
          onNavigateMode={navigateMode}
          theme={theme}
        />
      ) : mode === "review" ? (
        <p className={`text-sm ${headerMuted}`} role="status">
          Session required for merge review.
        </p>
      ) : null}

      {mode === "habitat" && (
        <HabitatModeView
          snapshot={snapshot}
          pet={pet}
          meters={meters}
          thoughts={thoughts}
          resting={resting}
          onRunAction={runAction}
          onNavigateMode={navigateMode}
          recommendedMode={recommended}
          theme={theme}
          stream={stream}
          files={files}
          events={events}
          newFilePaths={newFilePaths}
        />
      )}
    </div>
  );
}
