"use client";

import { SANCTUARY_TABS, type SanctuaryTab } from "@/lib/joyzone";
import { cn } from "@/lib/cn";

export function HabitatNav({
  active,
  onChange,
  reviewPulse,
}: {
  active: SanctuaryTab;
  onChange: (tab: SanctuaryTab) => void;
  reviewPulse?: boolean;
}) {
  return (
    <nav
      className="fixed bottom-0 left-0 right-0 z-40 border-t border-cozy-elevated/80 bg-cozy-night/95 backdrop-blur-lg pb-[env(safe-area-inset-bottom)]"
      aria-label="Sanctuary"
    >
      <ul className="mx-auto flex max-w-xl gap-0.5 overflow-x-auto px-1 py-1.5 scrollbar-none">
        {SANCTUARY_TABS.map((tab) => {
          const isActive = active === tab.id;
          return (
            <li key={tab.id} className="shrink-0 flex-1 min-w-[52px]">
              <button
                type="button"
                onClick={() => onChange(tab.id)}
                aria-current={isActive ? "page" : undefined}
                className={cn(
                  "flex w-full flex-col items-center gap-0.5 rounded-2xl px-1 py-2 text-[9px] font-semibold sm:text-[10px]",
                  isActive
                    ? "bg-cozy-sage/20 text-cozy-sage"
                    : "text-cozy-muted hover:bg-cozy-elevated/50 hover:text-cozy-cream",
                )}
              >
                <span className="relative text-base sm:text-lg">
                  {tab.icon}
                  {tab.id === "review" && reviewPulse && !isActive && (
                    <span className="absolute -right-0.5 -top-0.5 h-2 w-2 rounded-full bg-cozy-peach" />
                  )}
                </span>
                <span className="truncate">{tab.label}</span>
              </button>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
