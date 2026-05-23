import type { BoardTask } from "./kanban";
import { buildPipelineStages } from "./kanban";
import type { JoyEvent, LiveTaskSnapshot } from "./types";

export interface CareMeters {
  clarity: number;
  energy: number;
  confidence: number;
}

function clamp(n: number) {
  return Math.max(0, Math.min(100, Math.round(n)));
}

export function computeCareMeters(
  snapshot: LiveTaskSnapshot,
  boardTasks: BoardTask[],
  pulseTicks: number,
): CareMeters {
  const d = snapshot.display;
  const act = d?.activityState ?? "none";
  const lease = snapshot.leaseStatus;
  const stages = buildPipelineStages(snapshot);
  const completeStages = stages.filter((s) => s.state === "complete").length;
  const convergence = clamp((completeStages / Math.max(stages.length, 1)) * 100);

  const deliverables = d?.deliverables ?? [];
  const doneDel = deliverables.filter((x) => x.done).length;
  const deliverableScore =
    deliverables.length === 0
      ? convergence * 0.5
      : clamp((doneDel / deliverables.length) * 100);

  const fractured =
    Boolean(snapshot.blockedReason) || act === "blocked" || lease === "Blocked";

  const baseProgress = d?.progressPercent ?? 0;
  const pulseBoost = Math.min(8, pulseTicks % 12);
  const isActive = act === "active" || lease === "Running";
  const energy = clamp(
    baseProgress + pulseBoost + (isActive ? 10 : 0) + (snapshot.progress?.filesCopiedThisTick ?? 0) * 4,
  );

  const hasPhase = Boolean(d?.phaseLabel || d?.currentStepTitle);
  const hasHeadline = Boolean(d?.headline && d.headline.length > 3);
  let clarity = clamp(
    (hasPhase ? 35 : 10) +
      (hasHeadline ? 25 : 0) +
      convergence * 0.35 +
      (snapshot.title ? 15 : 0),
  );
  if (fractured) clarity = clamp(clarity * 0.45);
  if (act === "waiting" || !lease) clarity = clamp(clarity * 0.7);

  let confidence = fractured
    ? 22
    : act === "stuck"
      ? 40
      : isActive
        ? 84
        : act === "review"
          ? 92
          : 58;
  confidence = clamp((confidence + deliverableScore) / 2);

  const activeTasks = boardTasks.filter((t) => t.status === 2).length;
  if (activeTasks > 3) confidence = clamp(confidence - 8);

  return { clarity, energy, confidence };
}

/** @deprecated habitat vitals — use computeCareMeters */
export function computeVitals(
  snapshot: LiveTaskSnapshot,
  boardTasks: BoardTask[],
  pulseTicks: number,
  _preferredBiome?: string,
) {
  const meters = computeCareMeters(snapshot, boardTasks, pulseTicks);
  const act = snapshot.display?.activityState ?? "none";
  const weather =
    act === "active"
      ? ("rain" as const)
      : act === "blocked"
        ? ("eclipse" as const)
        : act === "done" || act === "review"
          ? ("bloom" as const)
          : ("night" as const);
  return {
    momentum: meters.energy,
    convergence: meters.clarity,
    harmony: meters.confidence,
    confidence: meters.confidence,
    flow: meters.energy,
    synthesisCharge: meters.energy,
    weather,
    weatherLabel: "",
    weatherMood: "",
    weatherEmoji: "",
    phaseWhisper: snapshot.display?.headline ?? "",
    intentEcho: snapshot.title ?? "Run",
    companionCount: 1,
    criticalConvergence: false,
    stabilizing: snapshot.display?.activityState === "review",
    resonanceFracture: Boolean(snapshot.blockedReason),
    fractureWhisper: snapshot.blockedReason,
    isActive: snapshot.display?.activityState === "active",
    blooming: false,
  };
}

export function buildMemoryParticles(
  events: JoyEvent[],
  recentActivity: string[],
  pulseTicks: number,
): string[] {
  const fromEvents = events.slice(-6).map((e) => e.summary ?? e.Summary ?? "");
  const fromRecent = recentActivity.slice(-4);
  if (pulseTicks > 0 && fromEvents.length === 0) {
    fromEvents.push("Orchestration pulse received.");
  }
  return [...fromEvents, ...fromRecent].filter(Boolean).slice(-8);
}

export function friendlyFracture(blockedReason: string | null): string | null {
  if (!blockedReason) return null;
  if (blockedReason.length > 120) return `${blockedReason.slice(0, 117)}…`;
  return blockedReason;
}
