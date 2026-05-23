"use client";

import { ArrowRight } from "lucide-react";
import type { JoyZoningOperationalMode } from "@/lib/operational-modes";
import { OPERATIONAL_MODES } from "@/lib/operational-modes";

export function ModeHandoffLink({
  targetMode,
  label,
  reason,
  onNavigate,
  theme = "pet",
}: {
  targetMode: JoyZoningOperationalMode;
  label?: string;
  reason?: string;
  onNavigate: (mode: JoyZoningOperationalMode) => void;
  theme?: "pet" | "campfire";
}) {
  const meta = OPERATIONAL_MODES[targetMode];
  const text = theme === "pet" ? "text-pet-mint" : "text-campfire-accent";
  const border = theme === "pet" ? "border-pet-elevated" : "border-campfire-border";

  return (
    <button
      type="button"
      onClick={() => onNavigate(targetMode)}
      className={`flex w-full items-start gap-2 rounded-lg border ${border} px-3 py-2 text-left text-xs hover:opacity-90`}
    >
      <ArrowRight className={`mt-0.5 h-3.5 w-3.5 shrink-0 ${text}`} />
      <span>
        <span className={`font-semibold ${text}`}>{label ?? `Open ${meta.title}`}</span>
        {reason && <span className="mt-0.5 block text-[10px] opacity-70">{reason}</span>}
      </span>
    </button>
  );
}
