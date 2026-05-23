"use client";

import { motion } from "framer-motion";
import { ArrowRight, Check, Circle, X } from "lucide-react";
import { cn } from "@/lib/cn";
import { buildPipelineStages } from "@/lib/kanban";
import type { LiveTaskSnapshot } from "@/lib/types";

function StageNode({
  icon,
  label,
  state,
}: {
  icon: string;
  label: string;
  state: "pending" | "current" | "complete" | "failed";
}) {
  return (
    <div className="flex flex-col items-center gap-1.5">
      <div
        className={cn(
          "relative flex h-11 w-11 items-center justify-center rounded-xl border-2 text-lg transition-all sm:h-12 sm:w-12 sm:text-xl",
          state === "complete" && "border-campfire-ok bg-campfire-ok/20",
          state === "current" &&
            "border-campfire-accent bg-campfire-accent/20 shadow-[0_0_20px_rgba(255,140,66,0.35)]",
          state === "failed" && "border-campfire-err bg-campfire-err/20",
          state === "pending" &&
            "border-campfire-border bg-campfire-elevated opacity-55",
        )}
      >
        <span aria-hidden>{icon}</span>
        {state === "current" && (
          <motion.span
            className="absolute -inset-1 rounded-xl border-2 border-campfire-accent/50"
            animate={{ opacity: [0.4, 1, 0.4] }}
            transition={{ repeat: Infinity, duration: 1.8 }}
          />
        )}
        <span className="absolute -bottom-1 -right-1 flex h-4 w-4 items-center justify-center rounded-full bg-campfire-surface">
          {state === "complete" && (
            <Check className="h-3 w-3 text-campfire-ok" strokeWidth={3} />
          )}
          {state === "failed" && (
            <X className="h-3 w-3 text-campfire-err" strokeWidth={3} />
          )}
          {state === "current" && (
            <Circle className="h-2 w-2 fill-campfire-accent text-campfire-accent" />
          )}
        </span>
      </div>
      <span
        className={cn(
          "text-center text-[10px] font-semibold uppercase tracking-wide",
          state === "current" && "text-campfire-accent",
          state === "complete" && "text-campfire-ok",
          state === "failed" && "text-campfire-err",
          state === "pending" && "text-campfire-muted",
        )}
      >
        {label}
      </span>
    </div>
  );
}

export function PipelineFlow({ snapshot }: { snapshot: LiveTaskSnapshot }) {
  const stages = buildPipelineStages(snapshot);
  const currentIdx = stages.findIndex(
    (s) => s.state === "current" || s.state === "failed",
  );

  return (
    <section
      aria-label="Execution pipeline"
      className="rounded-2xl border border-campfire-border bg-campfire-surface p-4"
    >
      <h3 className="mb-4 text-xs font-bold uppercase tracking-widest text-campfire-muted">
        Task handoff pipeline
      </h3>

      <div className="flex items-center justify-center gap-0.5 overflow-x-auto sm:gap-1">
        {stages.map((stage, i) => (
          <div key={stage.id} className="flex items-center">
            <StageNode icon={stage.icon} label={stage.label} state={stage.state} />
            {i < stages.length - 1 && (
              <ArrowRight
                className={cn(
                  "mx-0.5 h-4 w-4 shrink-0 sm:mx-1",
                  i < currentIdx ||
                    (currentIdx === -1 && stage.state === "complete")
                    ? "text-campfire-ok"
                    : "text-campfire-border",
                )}
                aria-hidden
              />
            )}
          </div>
        ))}
      </div>

      {snapshot.leaseStatus && (
        <p className="mt-4 text-center text-xs text-campfire-muted">
          <span className="font-semibold text-campfire-text">{snapshot.leaseStatus}</span>
          {snapshot.display?.phaseLabel && (
            <>
              {" → "}
              <span className="text-campfire-accent">{snapshot.display.phaseLabel}</span>
            </>
          )}
        </p>
      )}
    </section>
  );
}
