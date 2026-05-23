"use client";

import { MODE_ORDER, OPERATIONAL_MODES, type JoyZoningOperationalMode } from "@/lib/operational-modes";

const CONSOLE_MODES = MODE_ORDER.filter((m) => m !== "habitat");

export function ModeEmphasisBar({
  emphasis,
  recommendedMode,
  onEmphasis,
}: {
  emphasis: JoyZoningOperationalMode;
  recommendedMode?: string | null;
  onEmphasis: (mode: JoyZoningOperationalMode) => void;
}) {
  return (
    <nav
      className="rounded-xl border border-zinc-700 bg-zinc-900/80 p-2"
      aria-label="Section emphasis"
      data-testid="mode-emphasis-bar"
    >
      <p className="mb-2 px-1 text-[10px] font-semibold uppercase tracking-widest text-zinc-500">
        Focus (scroll) — all sections stay visible
      </p>
      <div className="flex flex-wrap gap-1.5" role="tablist">
        {CONSOLE_MODES.map((slug) => {
          const meta = OPERATIONAL_MODES[slug];
          const active = emphasis === slug;
          const recommended = recommendedMode === slug;
          return (
            <button
              key={slug}
              type="button"
              role="tab"
              aria-selected={active}
              onClick={() => onEmphasis(slug)}
              className={`rounded-lg border px-3 py-2 text-left text-xs transition-colors ${
                active
                  ? "border-sky-500/60 bg-sky-500/15 text-sky-200"
                  : "border-transparent text-zinc-400 hover:border-zinc-600 hover:text-zinc-200"
              }`}
            >
              <span className="font-bold">{meta.title}</span>
              {recommended && (
                <span className="ml-1.5 text-[9px] font-semibold uppercase text-amber-400">
                  recommended
                </span>
              )}
            </button>
          );
        })}
      </div>
    </nav>
  );
}
