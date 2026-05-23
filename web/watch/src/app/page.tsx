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
      <main className="relative mx-auto max-w-4xl px-4 pt-4 sm:px-6">
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
        <div className="flex min-h-screen flex-col items-center justify-center gap-3 text-zinc-500">
          <p className="text-2xl font-bold text-sky-400 animate-pulse">◇</p>
          <p>Loading operator console…</p>
        </div>
      }
    >
      <WatchPageInner />
    </Suspense>
  );
}
