"use client";

import { useState } from "react";
import type { ActiveTaskSummary, WatchSession } from "@/lib/types";

export function OperatorEntryFlow({
  sessions,
  activeTasks,
  sessionId,
  taskId,
  taskOptions,
  onSessionChange,
  onTaskChange,
  onEnter,
}: {
  sessions: WatchSession[];
  activeTasks: ActiveTaskSummary[];
  sessionId: string;
  taskId: string;
  taskOptions: { id: string; title: string; status: number }[];
  onSessionChange: (id: string) => void;
  onTaskChange: (id: string) => void;
  onEnter: () => void;
}) {
  const [step, setStep] = useState<"pick" | "confirm">("pick");

  return (
    <div className="mx-auto max-w-lg space-y-6 px-1 pb-8 text-zinc-100">
      <header className="text-center">
        <p className="text-[10px] font-semibold uppercase tracking-widest text-zinc-500">
          JoyZoning Watch
        </p>
        <h1 className="mt-2 text-2xl font-bold">Operator console</h1>
        <p className="mt-2 text-sm text-zinc-400">
          One screen — task status, workers, merge actions, and live events. No mode hunting.
        </p>
      </header>

      {step === "pick" && (
        <div className="space-y-4 rounded-2xl border border-zinc-700 bg-zinc-900/60 p-5">
          <label className="block">
            <span className="text-xs font-medium text-zinc-500">Workspace session</span>
            <select
              className="mt-1 w-full rounded-lg border border-zinc-600 bg-zinc-950 px-3 py-2.5 text-sm"
              value={sessionId}
              onChange={(e) => {
                onSessionChange(e.target.value);
                onTaskChange("");
              }}
            >
              <option value="">Choose session…</option>
              {sessions.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name || s.workspaceRoot}
                </option>
              ))}
            </select>
          </label>

          <label className="block">
            <span className="text-xs font-medium text-zinc-500">Task</span>
            <select
              className="mt-1 w-full rounded-lg border border-zinc-600 bg-zinc-950 px-3 py-2.5 text-sm"
              value={taskId}
              onChange={(e) => onTaskChange(e.target.value)}
              disabled={!sessionId}
            >
              <option value="">Choose task…</option>
              {taskOptions.map((t) => (
                <option key={t.id} value={t.id}>
                  {(t.title || t.id).slice(0, 60)}
                </option>
              ))}
            </select>
          </label>

          {activeTasks.length > 0 && (
            <div className="flex flex-wrap gap-2">
              <span className="w-full text-[10px] uppercase tracking-widest text-zinc-500">
                Active runs
              </span>
              {activeTasks.slice(0, 6).map((t) => (
                <button
                  key={t.taskId}
                  type="button"
                  onClick={() => {
                    onSessionChange(t.sessionId);
                    onTaskChange(t.taskId);
                  }}
                  className="rounded-full border border-sky-500/40 bg-sky-500/10 px-3 py-1 text-xs text-sky-200"
                >
                  {t.title.slice(0, 28)}
                  {t.title.length > 28 ? "…" : ""} · {t.leaseStatus}
                </button>
              ))}
            </div>
          )}

          <button
            type="button"
            disabled={!taskId}
            onClick={() => setStep("confirm")}
            className="w-full rounded-xl bg-sky-600 py-3 text-sm font-bold text-white disabled:opacity-40"
          >
            Continue
          </button>
        </div>
      )}

      {step === "confirm" && (
        <div className="space-y-4 rounded-2xl border border-zinc-700 bg-zinc-900/60 p-5 text-center">
          <p className="text-sm text-zinc-400">
            Open the unified console for this task. Approve, revoke, and workspace actions stay on
            one page.
          </p>
          <button
            type="button"
            onClick={onEnter}
            className="w-full rounded-xl bg-sky-600 py-3 text-sm font-bold text-white"
          >
            Open console
          </button>
          <button
            type="button"
            onClick={() => setStep("pick")}
            className="text-xs text-zinc-500 hover:text-zinc-300"
          >
            Back
          </button>
        </div>
      )}
    </div>
  );
}
