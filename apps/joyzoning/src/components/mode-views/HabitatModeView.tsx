"use client";

import type { JoyEvent, LiveTaskSnapshot, StreamLine } from "@/lib/types";
import type { PetActionId, PetState } from "@/lib/pet";
import type { CareMeters as CareMetersState } from "@/lib/vitals";
import { SynthesisPet } from "../pet/SynthesisPet";
import { CareMeters } from "../pet/CareMeters";
import { ThoughtBubbles } from "../pet/ThoughtBubbles";
import { PetProgressRing } from "../pet/PetProgressRing";
import { SickBanner } from "../pet/SickBanner";
import { ModeHandoffLink } from "../ModeHandoffLink";
import { CampfireAmbientExtras } from "../habitat/CampfireAmbientExtras";
import type { JoyZoningOperationalMode } from "@/lib/operational-modes";
import { OPERATIONAL_MODES } from "@/lib/operational-modes";

export function HabitatModeView({
  snapshot,
  pet,
  meters,
  thoughts,
  resting,
  onRunAction,
  onNavigateMode,
  recommendedMode,
  theme = "pet",
  stream = [],
  files = [],
  events = [],
  newFilePaths,
}: {
  snapshot: LiveTaskSnapshot;
  pet: PetState;
  meters: CareMetersState;
  thoughts: string[];
  resting: boolean;
  onRunAction: (id: PetActionId) => void;
  onNavigateMode: (mode: JoyZoningOperationalMode) => void;
  recommendedMode?: string | null;
  theme?: "pet" | "campfire";
  stream?: StreamLine[];
  files?: string[];
  events?: JoyEvent[];
  newFilePaths?: Set<string>;
}) {
  const headline = snapshot.display?.headline ?? snapshot.title ?? "Synthesis run";
  const sub = snapshot.display?.subheadline;
  const needsReview = snapshot.leaseStatus === "ReadyForReview";
  const needsExecution =
    snapshot.leaseStatus === "Running" ||
    snapshot.leaseStatus === "Leased" ||
    snapshot.leaseStatus === "Blocked" ||
    Boolean(snapshot.blockedReason);

  return (
    <div data-joyzoning-mode="habitat" data-canonical-surface="false" className="space-y-4">
      <header>
        <h2 className="text-sm font-bold text-pet-cream">{OPERATIONAL_MODES.habitat.title}</h2>
        <p className="text-xs text-pet-muted">{OPERATIONAL_MODES.habitat.primaryQuestion}</p>
        <p className="mt-1 text-[11px] text-amber-200/80">
          Ambient layer only — use handoffs below for authoritative actions.
        </p>
      </header>

      <div className="pet-panel p-3">
        <p className="truncate text-sm font-semibold text-pet-cream">{headline}</p>
        {sub && <p className="truncate text-xs text-pet-muted">{sub}</p>}
      </div>

      {resting && (
        <p className="rounded-pet bg-pet-deep/80 px-3 py-2 text-center text-xs text-pet-muted">
          Rest mode — polling paused. Wake the pet to resume live updates.
        </p>
      )}

      <SickBanner
        pet={pet}
        onStackTrace={() => onNavigateMode("execution")}
        onRetry={() => onRunAction("retry")}
      />

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
          onClick={() => onRunAction(pet.primaryAction.id)}
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
                onClick={() => {
                  if (a.id === "review") {
                    onNavigateMode("review");
                    return;
                  }
                  if (a.id === "stack") {
                    onNavigateMode("execution");
                    return;
                  }
                  onRunAction(a.id);
                }}
                className="rounded-pet pet-card px-3 py-2 text-xs text-pet-muted hover:text-pet-cream"
              >
                {a.label}
              </button>
            ))}
          </div>
        )}
      </div>

      <div className="space-y-2 border-t border-pet-elevated/50 pt-4">
        <p className="text-[10px] font-semibold uppercase tracking-widest text-pet-muted">
          Go deeper
        </p>
        {needsExecution && (
          <ModeHandoffLink
            targetMode="execution"
            label="Open Execution"
            reason={snapshot.blockedReason ? "Investigate blocked worker" : "Watch live worker activity"}
            onNavigate={onNavigateMode}
          />
        )}
        {needsReview && (
          <ModeHandoffLink
            targetMode="review"
            label="Open Review"
            reason="Merge queue and approve/revoke"
            onNavigate={onNavigateMode}
          />
        )}
        {recommendedMode && recommendedMode !== "habitat" && (
          <ModeHandoffLink
            targetMode={recommendedMode as JoyZoningOperationalMode}
            label={`Suggested: ${OPERATIONAL_MODES[recommendedMode as JoyZoningOperationalMode]?.title ?? recommendedMode}`}
            reason="Based on current lease and worker state"
            onNavigate={onNavigateMode}
          />
        )}
        <ModeHandoffLink
          targetMode="planning"
          label="Open Planning"
          reason="Session kanban and backlog"
          onNavigate={onNavigateMode}
        />
      </div>

      {theme === "campfire" && (
        <CampfireAmbientExtras
          snapshot={snapshot}
          stream={stream}
          files={files}
          events={events}
          newFilePaths={newFilePaths}
        />
      )}
    </div>
  );
}
