"use client";

import { Suspense, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { ArrowLeft } from "lucide-react";
import { WatchApp } from "@/components/WatchApp";

function ConsoleInner() {
  const searchParams = useSearchParams();
  const initialTaskId = searchParams.get("taskId") ?? "";
  const [watching, setWatching] = useState(Boolean(initialTaskId));
  const [headerState, setHeaderState] = useState({
    connLabel: "Connecting…",
    connVariant: "idle" as "live" | "error" | "idle",
  });

  return (
    <div className="flex h-screen flex-col bg-zinc-950 text-zinc-100">
      <header className="flex items-center gap-3 border-b border-zinc-800 px-4 py-3">
        <Link
          href="/"
          className="inline-flex items-center gap-1.5 rounded-lg px-2 py-1 text-sm text-zinc-400 hover:bg-zinc-900 hover:text-zinc-100"
        >
          <ArrowLeft className="h-4 w-4" />
          Back to chat
        </Link>
        <div className="min-w-0 flex-1">
          <h1 className="text-sm font-semibold">Operator console</h1>
          <p className="truncate text-xs text-zinc-500">
            Drill-down view for execution, merge review, and workspace actions.
          </p>
        </div>
        <span className="text-xs text-zinc-500">{headerState.connLabel}</span>
      </header>
      <main className="relative mx-auto w-full max-w-5xl flex-1 overflow-y-auto px-4 py-4 sm:px-6">
        <WatchApp
          initialTaskId={initialTaskId}
          watching={watching}
          onWatchingChange={setWatching}
          onHeaderChange={setHeaderState}
        />
      </main>
    </div>
  );
}

export default function ConsolePage() {
  return (
    <Suspense
      fallback={
        <div className="flex h-screen items-center justify-center bg-zinc-950 text-zinc-500">
          Loading operator console…
        </div>
      }
    >
      <ConsoleInner />
    </Suspense>
  );
}
