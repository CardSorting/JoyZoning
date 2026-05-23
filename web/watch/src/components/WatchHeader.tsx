"use client";

import { ConnectionPill } from "./ConnectionPill";

export function WatchHeader({
  statusLine,
  variant,
}: {
  statusLine: string;
  variant: "live" | "error" | "idle";
}) {
  return (
    <header className="sticky top-0 z-30 border-b border-zinc-800 bg-zinc-950/92 backdrop-blur-md">
      <div className="mx-auto flex max-w-4xl items-center justify-between gap-3 px-4 py-3">
        <div>
          <p className="text-[10px] font-semibold uppercase tracking-wider text-zinc-500">
            JoyZoning
          </p>
          <h1 className="text-base font-bold text-zinc-100">Operator Watch</h1>
        </div>
        <ConnectionPill label={statusLine} variant={variant} />
      </div>
    </header>
  );
}
