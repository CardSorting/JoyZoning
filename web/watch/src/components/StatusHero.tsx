"use client";

import { motion } from "framer-motion";
import { activityLabel, isIdle, statusIcon } from "@/lib/presentation";
import { leaseColumnLabel } from "@/lib/kanban";
import type { LiveTaskSnapshot } from "@/lib/types";
import { cn } from "@/lib/cn";
import { ProgressRing } from "./ProgressRing";

export function StatusHero({ snapshot }: { snapshot: LiveTaskSnapshot }) {
  const d = snapshot.display;
  const idle = isIdle(snapshot);
  const pct = Math.max(0, Math.min(100, d.progressPercent ?? 0));
  const act = d.activityState || "waiting";
  const icon = statusIcon(act, snapshot.leaseStatus);
  const kanbanCol = leaseColumnLabel(snapshot.leaseStatus);

  return (
    <motion.article
      initial={{ opacity: 0, y: 12 }}
      animate={{ opacity: 1, y: 0 }}
      className="rounded-2xl border border-campfire-border bg-campfire-surface p-5 shadow-glow"
    >
      <div className="flex items-center gap-4">
        <div
          className="flex h-16 w-16 shrink-0 items-center justify-center rounded-2xl bg-campfire-elevated text-4xl"
          aria-hidden
        >
          {icon}
        </div>

        <div className="min-w-0 flex-1">
          <h2 className="truncate text-xl font-bold tracking-tight text-campfire-text sm:text-2xl">
            {idle ? snapshot.title || "Your build" : d.headline}
          </h2>
          {!idle && d.subheadline && (
            <p className="mt-1 line-clamp-2 text-sm text-campfire-muted">
              {d.subheadline}
            </p>
          )}
          <div className="mt-2 flex flex-wrap items-center gap-2">
            <span
              className={cn(
                "rounded-full px-2.5 py-0.5 text-xs font-bold uppercase tracking-wide",
                act === "active" && "bg-campfire-ok/15 text-campfire-ok",
                (act === "blocked" || act === "stuck") &&
                  "bg-campfire-err/15 text-campfire-err",
                (act === "waiting" || act === "idle") &&
                  "bg-campfire-warn/15 text-campfire-warn",
                act === "review" && "bg-campfire-accent/15 text-campfire-accent",
              )}
            >
              {activityLabel(act)}
            </span>
            {d.phaseLabel && (
              <span className="text-xs text-campfire-muted">
                {d.phaseLabel}
                {d.stepProgressLabel ? ` · ${d.stepProgressLabel}` : ""}
              </span>
            )}
            {!idle && (
              <span className="text-xs text-campfire-muted">
                · board: <span className="text-campfire-accent">{kanbanCol}</span>
              </span>
            )}
          </div>
        </div>

        {!idle && (
          <div className="hidden shrink-0 sm:block">
            <ProgressRing percent={pct} />
          </div>
        )}
      </div>

      {!idle && (
        <div
          className="mt-4 h-1.5 overflow-hidden rounded-full bg-campfire-elevated"
          role="progressbar"
          aria-valuenow={pct}
          aria-valuemin={0}
          aria-valuemax={100}
        >
          <motion.div
            className="h-full rounded-full bg-gradient-to-r from-campfire-accent-dim to-campfire-accent"
            initial={{ width: 0 }}
            animate={{ width: `${pct}%` }}
            transition={{ duration: 0.5 }}
          />
        </div>
      )}
    </motion.article>
  );
}
