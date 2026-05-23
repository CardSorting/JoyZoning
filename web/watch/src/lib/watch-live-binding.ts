/**
 * Branded live binding — Watch cannot render operator chrome from placeholder props.
 * Build via createWatchLiveBinding() from useLiveTask + session state only.
 */

import {
  isOperationalMode,
  type JoyZoningOperationalMode,
} from "./operational-modes";
import type { WatchOperatorShellProps } from "@/components/watch-operator-shell-props";
import type { LiveTaskSnapshot } from "./types";

type ModeTransition = {
  targetMode: JoyZoningOperationalMode;
  label: string;
  reason: string;
  handoffKind?: string | null;
};

export type WatchLiveBindingInput = WatchOperatorShellProps;

export type WatchLiveBinding = WatchLiveBindingInput & {
  readonly __watchLiveBinding: true;
};

export type WatchLiveBindingResult =
  | { ok: true; binding: WatchLiveBinding }
  | { ok: false; error: string };

const INVALID_CONN_LABELS = new Set(["", "Error"]);

export function createWatchLiveBinding(input: WatchLiveBindingInput): WatchLiveBinding {
  const result = tryCreateWatchLiveBinding(input);
  if (!result.ok) {
    throw new Error(result.error);
  }
  return result.binding;
}

export function tryCreateWatchLiveBinding(
  input: WatchLiveBindingInput,
): WatchLiveBindingResult {
  const sessionId = input.sessionId?.trim();
  if (!sessionId) {
    return { ok: false, error: "Session id is required for live operator binding." };
  }

  const snapshot = input.snapshot;
  if (!snapshot?.taskId?.trim()) {
    return { ok: false, error: "Live task snapshot is required." };
  }

  if (!snapshot.display?.headline && !snapshot.title?.trim()) {
    return { ok: false, error: "Snapshot is missing display metadata." };
  }

  const conn = input.connLabel?.trim() ?? "";
  if (INVALID_CONN_LABELS.has(conn)) {
    return {
      ok: false,
      error:
        conn === "Error"
          ? "Connection is in error state — reconnect before opening the operator shell."
          : "Connection label is missing — bind from useLiveTask.connLabel, not a placeholder.",
    };
  }

  if (typeof input.onNudge !== "function" || typeof input.onDepart !== "function") {
    return { ok: false, error: "Live handlers onNudge and onDepart are required." };
  }

  if (typeof input.onRestingChange !== "function") {
    return { ok: false, error: "onRestingChange must come from Watch session state." };
  }

  return {
    ok: true,
    binding: {
      ...input,
      sessionId,
      snapshot,
      connLabel: conn,
      __watchLiveBinding: true,
    },
  };
}

export function isWatchLiveBinding(value: unknown): value is WatchLiveBinding {
  return (
    typeof value === "object" &&
    value !== null &&
    (value as WatchLiveBinding).__watchLiveBinding === true
  );
}

function addTransition(
  list: ModeTransition[],
  current: JoyZoningOperationalMode,
  target: JoyZoningOperationalMode,
  label: string,
  reason: string,
  handoffKind: string,
) {
  if (current === target) return;
  if (list.some((t) => t.targetMode === target)) return;
  list.push({ targetMode: target, label, reason, handoffKind });
}

function addReviewTransition(
  list: ModeTransition[],
  current: JoyZoningOperationalMode,
  label: string,
  reason: string,
  handoffKind: string,
) {
  if (list.some((t) => t.targetMode === "review")) return;
  list.push({
    targetMode: "review",
    label: current === "review" ? "Review queue" : label,
    reason,
    handoffKind,
  });
}

/** Client fallback aligned with OperationalModeNavigation.BuildTransitionsForLive. */
export function buildInferredLiveTransitions(
  recommended: JoyZoningOperationalMode,
  leaseStatus?: string | null,
): ModeTransition[] {
  const transitions: ModeTransition[] = [];
  addTransition(
    transitions,
    recommended,
    "planning",
    "View on board",
    "Session kanban and task intent",
    "planning_task",
  );
  addTransition(
    transitions,
    recommended,
    "execution",
    "Watch execution",
    "What workers are doing right now",
    "execution_live",
  );

  const reviewReady =
    leaseStatus === "ReadyForReview" || leaseStatus === "Verifying";
  if (reviewReady) {
    addReviewTransition(
      transitions,
      recommended,
      "Review for merge",
      "Changed files and approve/revoke",
      "review_task",
    );
  }

  addTransition(
    transitions,
    recommended,
    "habitat",
    "Ambient glance",
    "Workspace atmosphere",
    "habitat_ambient",
  );
  return transitions;
}

/** Infer mode navigation when backend payload is partial (never silently pretend it was complete). */
export function inferModeNavigationFromSnapshot(snapshot: LiveTaskSnapshot) {
  const leaseStatus = snapshot.leaseStatus;
  let recommendedMode: JoyZoningOperationalMode = "habitat";
  if (leaseStatus === "ReadyForReview" || leaseStatus === "Verifying") {
    recommendedMode = "review";
  } else if (
    leaseStatus === "Running" ||
    leaseStatus === "Leased" ||
    leaseStatus === "Blocked" ||
    Boolean(snapshot.blockedReason)
  ) {
    recommendedMode = "execution";
  } else if (leaseStatus) {
    recommendedMode = "planning";
  }

  return {
    recommendedMode,
    availableTransitions: buildInferredLiveTransitions(recommendedMode, leaseStatus),
    inferred: true as const,
  };
}

export function resolveModeNavigation(snapshot: LiveTaskSnapshot) {
  const raw = snapshot.modeNavigation;
  if (raw?.recommendedMode && isOperationalMode(raw.recommendedMode)) {
    const transitions = (raw.availableTransitions ?? []).filter(
      (t) => t?.targetMode && isOperationalMode(t.targetMode) && t.label && t.reason,
    );
    return {
      recommendedMode: raw.recommendedMode,
      availableTransitions: transitions,
      inferred: false as const,
    };
  }
  return inferModeNavigationFromSnapshot(snapshot);
}
