"use client";

import { motion } from "framer-motion";
import { Package, Sparkles } from "lucide-react";
import { taskStatusName } from "@/lib/presentation";
import type { ActiveTaskSummary, WatchSession } from "@/lib/types";

export function WelcomePicker({
  sessions,
  activeTasks,
  sessionId,
  taskId,
  taskOptions,
  onSessionChange,
  onTaskChange,
  onStart,
}: {
  sessions: WatchSession[];
  activeTasks: ActiveTaskSummary[];
  sessionId: string;
  taskId: string;
  taskOptions: { id: string; title: string; status: number }[];
  onSessionChange: (id: string) => void;
  onTaskChange: (id: string) => void;
  onStart: () => void;
}) {
  return (
    <motion.section
      initial={{ opacity: 0, y: 16 }}
      animate={{ opacity: 1, y: 0 }}
      className="mx-auto max-w-lg rounded-3xl border border-campfire-border bg-campfire-surface p-8 shadow-glow"
    >
      <div className="mb-6 flex justify-center">
        <div className="rounded-2xl bg-campfire-accent/15 p-4 text-campfire-accent">
          <Package className="h-10 w-10" strokeWidth={1.5} />
        </div>
      </div>

      <h2 className="text-center text-2xl font-bold text-campfire-text">
        Which build would you like to follow?
      </h2>
      <p className="mt-3 text-center text-campfire-muted">
        Like tracking a package — you&apos;ll see progress, files appearing, and plain-language
        updates. No terminal required.
      </p>

      <ol className="mt-6 flex justify-center gap-2 text-xs font-medium text-campfire-muted" aria-hidden>
        <li className="rounded-full bg-campfire-ok/20 px-3 py-1 text-campfire-ok">1. Open Watch</li>
        <li className="rounded-full bg-campfire-accent/20 px-3 py-1 text-campfire-accent">2. Choose</li>
        <li className="rounded-full bg-campfire-elevated px-3 py-1">3. Follow along</li>
      </ol>

      <div className="mt-8 space-y-4">
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-campfire-muted">Your workspace</span>
          <select
            className="w-full rounded-xl border border-campfire-border bg-campfire-elevated px-4 py-3 text-campfire-text outline-none focus:border-campfire-accent focus:ring-1 focus:ring-campfire-accent"
            value={sessionId}
            onChange={(e) => onSessionChange(e.target.value)}
          >
            <option value="">Select workspace…</option>
            {sessions.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name} — {s.workspaceRoot}
              </option>
            ))}
          </select>
        </label>

        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-campfire-muted">Build task</span>
          <select
            className="w-full rounded-xl border border-campfire-border bg-campfire-elevated px-4 py-3 text-campfire-text outline-none focus:border-campfire-accent focus:ring-1 focus:ring-campfire-accent"
            value={taskId}
            onChange={(e) => onTaskChange(e.target.value)}
            disabled={!sessionId}
          >
            <option value="">Select task…</option>
            {taskOptions.map((t) => (
              <option key={t.id} value={t.id}>
                {(t.title || "Task").slice(0, 55)}
                {t.title && t.title.length > 55 ? "…" : ""} [{taskStatusName(t.status)}]
              </option>
            ))}
          </select>
        </label>
      </div>

      {activeTasks.length > 0 && (
        <div className="mt-6 rounded-xl border border-campfire-border bg-campfire-elevated/50 p-4">
          <p className="mb-2 flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-campfire-muted">
            <Sparkles className="h-3.5 w-3.5" />
            Builds in progress
          </p>
          <ul className="space-y-2">
            {activeTasks.slice(0, 4).map((t) => (
              <li key={t.taskId}>
                <button
                  type="button"
                  className="w-full rounded-lg px-3 py-2 text-left text-sm transition-colors hover:bg-campfire-accent/10"
                  onClick={() => {
                    onSessionChange(t.sessionId);
                    onTaskChange(t.taskId);
                  }}
                >
                  <span className="font-medium text-campfire-text">{t.title}</span>
                  <span className="ml-2 text-campfire-muted">{t.leaseStatus}</span>
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}

      <button
        type="button"
        disabled={!taskId}
        onClick={onStart}
        className="mt-8 w-full rounded-xl bg-gradient-to-b from-campfire-accent to-campfire-accent-dim py-3.5 text-base font-bold text-campfire-bg transition-opacity disabled:opacity-40"
      >
        Start watching →
      </button>
    </motion.section>
  );
}
