"use client";

import { Flame } from "lucide-react";
import { ConnectionPill } from "./ConnectionPill";

export function WatchHeader({
  connLabel,
  connVariant,
  showSwitch,
  onSwitch,
}: {
  connLabel: string;
  connVariant: "live" | "error" | "idle";
  showSwitch: boolean;
  onSwitch: () => void;
}) {
  return (
    <header className="sticky top-0 z-20 border-b border-campfire-border bg-campfire-bg/90 backdrop-blur-md">
      <div className="mx-auto flex max-w-6xl items-center justify-between gap-4 px-4 py-4 sm:px-6">
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-campfire-accent/20 text-campfire-accent">
            <Flame className="h-5 w-5" />
          </div>
          <div>
            <p className="text-[10px] font-bold uppercase tracking-widest text-campfire-muted">
              JoyZoning
            </p>
            <h1 className="text-lg font-bold text-campfire-text">Build Watch</h1>
          </div>
        </div>
        <div className="flex items-center gap-3">
          {showSwitch && (
            <button
              type="button"
              onClick={onSwitch}
              className="hidden text-sm text-campfire-muted hover:text-campfire-text sm:block"
            >
              Switch project
            </button>
          )}
          <ConnectionPill label={connLabel} variant={connVariant} />
        </div>
      </div>
    </header>
  );
}
