"use client";

import { MODE_ORDER, type JoyZoningOperationalMode } from "@/lib/operational-modes";
import { useOperationalModes } from "@/hooks/useOperationalModes";

export function ModeSwitcher({
  active,
  recommendedMode,
  onChange,
  theme = "pet",
}: {
  active: JoyZoningOperationalMode;
  recommendedMode?: string | null;
  onChange: (mode: JoyZoningOperationalMode) => void;
  theme?: "pet" | "campfire";
}) {
  const { modes, error: registryError } = useOperationalModes();
  const border = theme === "pet" ? "border-pet-elevated" : "border-campfire-border";
  const bg = theme === "pet" ? "bg-pet-deep/80" : "bg-campfire-elevated";
  const activeCls =
    theme === "pet"
      ? "bg-pet-mint/20 text-pet-mint border-pet-mint/40"
      : "bg-campfire-accent/20 text-campfire-accent border-campfire-accent/40";
  const idleCls = theme === "pet" ? "text-pet-muted hover:text-pet-cream" : "text-campfire-muted hover:text-campfire-text";

  return (
    <nav
      className={`rounded-2xl border ${border} ${bg} p-2`}
      aria-label="Operational mode"
    >
      <p className={`mb-2 px-1 text-[10px] font-semibold uppercase tracking-widest ${idleCls}`}>
        Operator mode
        {registryError && (
          <span className="ml-1 font-normal normal-case opacity-70">(local registry)</span>
        )}
      </p>
      <div className="grid grid-cols-2 gap-1.5 sm:grid-cols-4" role="tablist">
        {MODE_ORDER.map((slug) => {
          const meta = modes[slug];
          const isActive = active === slug;
          const isRecommended = recommendedMode === slug;
          return (
            <button
              key={slug}
              type="button"
              role="tab"
              aria-selected={isActive}
              onClick={() => onChange(slug)}
              className={`rounded-xl border px-2 py-2 text-left transition-colors ${
                isActive ? activeCls : `border-transparent ${idleCls}`
              }`}
            >
              <span className="block text-xs font-bold">{meta.title}</span>
              <span className="mt-0.5 block text-[10px] leading-tight opacity-80">
                {meta.primaryQuestion}
              </span>
              {isRecommended && !isActive && (
                <span className="mt-1 inline-block text-[9px] font-semibold uppercase text-amber-300">
                  Suggested
                </span>
              )}
              {!meta.isCanonicalOperationalSurface && (
                <span className="mt-0.5 block text-[9px] opacity-60">ambient</span>
              )}
            </button>
          );
        })}
      </div>
      <p className={`mt-2 px-1 text-[10px] leading-snug ${idleCls}`}>
        {modes[active].shortDescription}
      </p>
    </nav>
  );
}
