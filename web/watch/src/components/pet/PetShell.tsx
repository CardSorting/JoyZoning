"use client";

import { useCallback, useMemo, useState } from "react";
import type { BoardTask } from "@/lib/kanban";
import {
  buildPetState,
  buildThoughtBubbles,
  type PetActionId,
} from "@/lib/pet";
import { computeCareMeters } from "@/lib/vitals";
import type { JoyEvent, LiveTaskSnapshot, StreamLine } from "@/lib/types";
import { SynthesisPet } from "./SynthesisPet";
import { CareMeters } from "./CareMeters";
import { ThoughtBubbles } from "./ThoughtBubbles";
import { PetProgressRing } from "./PetProgressRing";
import { SickBanner } from "./SickBanner";
import { PetObservatory } from "./PetObservatory";

export function PetShell({
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
}: {
  snapshot: LiveTaskSnapshot;
  boardTasks: BoardTask[];
  events: JoyEvent[];
  stream: StreamLine[];
  files: string[];
  pulseTicks: number;
  connLabel: string;
  resting: boolean;
  onRestingChange: (v: boolean) => void;
  onNudge: () => void;
  onDepart: () => void;
  onSelectTask?: (id: string) => void;
}) {
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

  const openStack = useCallback(() => {
    setObservatoryOpen(true);
    setStackHighlight(true);
    requestAnimationFrame(() => {
      document.getElementById("pet-stack-trace")?.scrollIntoView({ behavior: "smooth" });
    });
    setTimeout(() => setStackHighlight(false), 2400);
  }, []);

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
      case "review":
      case "stack":
        setObservatoryOpen(true);
        if (id === "stack") openStack();
        break;
    }
  };

  const headline = snapshot.display?.headline ?? snapshot.title ?? "Synthesis run";
  const sub = snapshot.display?.subheadline;

  return (
    <div className="relative min-h-[calc(100vh-7rem)] space-y-4 pb-8">
      <header className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-semibold text-pet-cream">{headline}</p>
          {sub && <p className="truncate text-xs text-pet-muted">{sub}</p>}
        </div>
        <button
          type="button"
          onClick={onDepart}
          className="shrink-0 rounded-pet pet-card px-3 py-1.5 text-xs text-pet-muted hover:text-pet-cream"
        >
          Leave
        </button>
      </header>

      {resting && (
        <p className="rounded-pet bg-pet-deep/80 px-3 py-2 text-center text-xs text-pet-muted">
          Rest mode — polling paused on your side. Tap Rest again to wake the pet.
        </p>
      )}

      <SickBanner pet={pet} onStackTrace={openStack} onRetry={() => runAction("retry")} />

      <div className="pet-panel p-5 sm:p-6">
        <SynthesisPet mood={pet.mood} needsYou={pet.needsYou} />
        <div className="mt-6 border-t border-pet-elevated/50 pt-5">
          <PetProgressRing percent={pet.progress} phase={pet.phase} />
        </div>
      </div>

      <div className="pet-panel p-4">
        <p className="mb-3 text-xs font-semibold uppercase tracking-wide text-pet-muted">
          Care meters
        </p>
        <CareMeters meters={meters} />
      </div>

      <div className="pet-panel p-4">
        <ThoughtBubbles thoughts={thoughts} />
      </div>

      <div className="space-y-2">
        <button
          type="button"
          onClick={() => runAction(pet.primaryAction.id)}
          className="w-full rounded-pet-lg bg-pet-mint/90 py-3.5 text-sm font-bold text-pet-night shadow-pet"
        >
          {pet.primaryAction.label}
        </button>
        <p className="text-center text-[11px] text-pet-muted">{pet.primaryAction.hint}</p>

        {pet.secondaryActions.length > 0 && (
          <div className="flex flex-wrap justify-center gap-2 pt-1">
            {pet.secondaryActions.map((a) => (
              <button
                key={a.id}
                type="button"
                title={a.hint}
                onClick={() => runAction(a.id)}
                className="rounded-pet pet-card px-3 py-2 text-xs text-pet-muted hover:text-pet-cream"
              >
                {a.label}
              </button>
            ))}
          </div>
        )}
      </div>

      {boardTasks.length > 1 && onSelectTask && (
        <div className="pet-panel p-3">
          <label className="text-xs text-pet-muted">Switch run</label>
          <select
            className="mt-1 w-full rounded-pet border border-pet-elevated bg-pet-deep px-3 py-2 text-sm text-pet-cream"
            value={snapshot.taskId}
            onChange={(e) => onSelectTask(e.target.value)}
          >
            {boardTasks.map((t) => (
              <option key={t.id} value={t.id}>
                {(t.title || t.id).slice(0, 48)}
              </option>
            ))}
          </select>
        </div>
      )}

      <PetObservatory
        snapshot={snapshot}
        events={events}
        stream={stream}
        connLabel={connLabel}
        files={files}
        open={observatoryOpen}
        onOpenChange={setObservatoryOpen}
        scrollToStack={stackHighlight}
      />
    </div>
  );
}
