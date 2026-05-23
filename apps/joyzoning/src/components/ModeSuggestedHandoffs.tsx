"use client";

import { ModeHandoffLink } from "./ModeHandoffLink";
import {
  isOperationalMode,
  type JoyZoningOperationalMode,
} from "@/lib/operational-modes";

export function ModeSuggestedHandoffs({
  recommendedMode,
  transitions,
  inferred,
  activeMode,
  onNavigate,
  theme = "pet",
}: {
  recommendedMode: string;
  transitions: {
    targetMode: string;
    label: string;
    reason: string;
    handoffKind?: string | null;
  }[];
  inferred?: boolean;
  activeMode: JoyZoningOperationalMode;
  onNavigate: (mode: JoyZoningOperationalMode) => void;
  theme?: "pet" | "campfire";
}) {
  const filtered = transitions.filter(
    (t) =>
      isOperationalMode(t.targetMode) &&
      t.targetMode !== activeMode &&
      t.targetMode !== recommendedMode,
  );

  const showInferredHint = inferred && filtered.length === 0;

  if (filtered.length === 0 && !showInferredHint) return null;

  const muted = theme === "pet" ? "text-pet-muted" : "text-campfire-muted";

  return (
    <div className="space-y-1.5" data-testid="mode-suggested-handoffs">
      <p className={`text-[10px] font-semibold uppercase tracking-widest ${muted}`}>
        Suggested next
        {inferred && (
          <span className="ml-1 font-normal normal-case opacity-70">(inferred)</span>
        )}
      </p>
      {showInferredHint && (
        <p className={`text-[10px] ${muted}`}>
          Server mode hints unavailable — use the mode switcher for handoffs.
        </p>
      )}
      {filtered.slice(0, 3).map((t) => (
        <ModeHandoffLink
          key={`${t.targetMode}-${t.handoffKind ?? t.label}`}
          targetMode={t.targetMode as JoyZoningOperationalMode}
          label={t.label}
          reason={t.reason}
          onNavigate={onNavigate}
          theme={theme}
        />
      ))}
    </div>
  );
}
