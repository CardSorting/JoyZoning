"use client";

import { Suspense, useState } from "react";
import { useSearchParams } from "next/navigation";
import { WatchApp, WatchHeader } from "@/components/WatchApp";

function WatchPageInner() {
  const searchParams = useSearchParams();
  const initialTaskId = searchParams.get("taskId") ?? "";
  const [watching, setWatching] = useState(Boolean(initialTaskId));
  const [headerState, setHeaderState] = useState({
    connLabel: "Connecting…",
    connVariant: "idle" as "live" | "error" | "idle",
  });

  return (
    <>
      <WatchHeader
        statusLine={headerState.connLabel}
        variant={headerState.connVariant}
      />
      <main className="relative mx-auto max-w-2xl px-4 pt-4 sm:max-w-3xl sm:px-6">
        <WatchApp
          initialTaskId={initialTaskId}
          watching={watching}
          onWatchingChange={setWatching}
          onHeaderChange={setHeaderState}
        />
      </main>
    </>
  );
}

export default function HomePage() {
  return (
    <Suspense
      fallback={
        <div className="flex min-h-screen flex-col items-center justify-center gap-3 text-pet-muted">
          <span className="text-3xl font-bold text-pet-mint animate-pulse" aria-hidden>
            ◈
          </span>
          <p>Loading JoyZone Watch…</p>
        </div>
      }
    >
      <WatchPageInner />
    </Suspense>
  );
}
