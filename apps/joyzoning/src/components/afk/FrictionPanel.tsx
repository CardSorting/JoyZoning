"use client";

import { recoveryActions } from "@/lib/joyzone";
import type { LiveTaskSnapshot } from "@/lib/types";

export function FrictionPanel({
  snapshot,
  whisper,
  onRefresh,
  onObservatory,
}: {
  snapshot: LiveTaskSnapshot;
  whisper: string | null;
  onRefresh: () => void;
  onObservatory: () => void;
}) {
  const actions = recoveryActions(snapshot);
  const handlers: Record<string, () => void> = {
    Refresh: onRefresh,
    "Try again": onRefresh,
    Reconnect: onRefresh,
    Observatory: onObservatory,
    "Clarify intent": onObservatory,
    "Visit harvest": onObservatory,
  };

  return (
    <div className="cozy-panel border-cozy-rose/25 p-5" role="alert">
      <p className="text-center text-sm font-semibold text-cozy-rose">
        🌫️ A gentle pause in the village
      </p>
      <p className="mt-2 text-center text-sm text-cozy-muted">
        {whisper ?? "Nothing scary — the habitat just needs a little care."}
      </p>
      <div className="mt-4 flex flex-wrap justify-center gap-2">
        {actions.map((a) => (
          <button
            key={a.label}
            type="button"
            title={a.hint}
            onClick={handlers[a.label] ?? onRefresh}
            className={
              a.primary
                ? "rounded-cozy bg-cozy-peach/25 px-4 py-2 text-sm font-semibold text-cozy-peach"
                : "rounded-cozy cozy-card px-4 py-2 text-sm text-cozy-muted"
            }
          >
            {a.label}
          </button>
        ))}
      </div>
    </div>
  );
}
