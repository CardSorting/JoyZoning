"use client";

import { motion } from "framer-motion";
import { sproutLabelForIndex } from "@/lib/joyzone";
import { useReducedMotion } from "@/lib/useReducedMotion";
import type { BoardTask } from "@/lib/kanban";

export function IntentGarden({
  tasks,
  activeTaskId,
  growing,
  onSelect,
}: {
  tasks: BoardTask[];
  activeTaskId: string;
  growing: boolean;
  onSelect?: (id: string) => void;
}) {
  const reduced = useReducedMotion();
  const plots =
    tasks.length > 0
      ? tasks.map((t, i) => ({
          ...t,
          sprout: sproutLabelForIndex(i, t.title),
        }))
      : [{ id: "—", title: "Empty plot", status: 0, sprout: "Plant a seed" }];

  return (
    <div className="cozy-panel p-4">
      <div className="mb-3 flex items-center justify-between">
        <span className="text-xs font-semibold text-cozy-cream">Intent garden</span>
        {growing && !reduced && (
          <motion.span
            className="text-[10px] text-cozy-sage"
            animate={{ opacity: [0.5, 1, 0.5] }}
            transition={{ repeat: Infinity, duration: 1.5 }}
          >
            🌧️ growing…
          </motion.span>
        )}
      </div>

      <ul className="grid gap-2 sm:grid-cols-2">
        {plots.map((plot) => {
          const active = plot.id.toLowerCase() === activeTaskId.toLowerCase();
          return (
            <li key={plot.id}>
              <button
                type="button"
                disabled={!onSelect || plot.id === "—"}
                onClick={() => onSelect?.(plot.id)}
                className={`cozy-card flex w-full flex-col items-start gap-1 p-3 text-left transition-all ${
                  active
                    ? "border-cozy-sage/50 bg-cozy-sage/10 shadow-cozy"
                    : "hover:border-cozy-lavender/30"
                }`}
              >
                <span className="text-lg">{active ? "🌻" : "🌱"}</span>
                <span className="text-[10px] font-medium uppercase tracking-wide text-cozy-lavender">
                  {plot.sprout}
                </span>
                <span className="line-clamp-2 text-sm text-cozy-cream">
                  {plot.title || "Unnamed seed"}
                </span>
                {active && (
                  <span className="text-[10px] text-cozy-sage">You&apos;re watching this plot</span>
                )}
              </button>
            </li>
          );
        })}
      </ul>

      <p className="mt-3 text-center text-[10px] text-cozy-muted">
        Tap a plot to tend a different intent
      </p>
    </div>
  );
}
