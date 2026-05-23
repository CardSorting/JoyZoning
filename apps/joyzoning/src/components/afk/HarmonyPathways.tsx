"use client";

import { motion } from "framer-motion";
import type { PipelineStage } from "@/lib/kanban";

const STONES = [
  { id: "dispatch", label: "Intent", emoji: "🌱" },
  { id: "lease", label: "Claim", emoji: "🏡" },
  { id: "build", label: "Grow", emoji: "🌿" },
  { id: "verify", label: "Align", emoji: "🍃" },
  { id: "review", label: "Bloom", emoji: "🌸" },
];

export function HarmonyPathways({ stages }: { stages: PipelineStage[] }) {
  return (
    <div className="flex items-start justify-between gap-1 px-1">
      {stages.map((stage, i) => {
        const stone = STONES.find((s) => s.id === stage.id);
        const emoji = stone?.emoji ?? "✨";
        const label = stone?.label ?? stage.label;
        return (
          <div key={stage.id} className="relative flex flex-1 flex-col items-center">
            {i > 0 && (
              <div
                className="absolute right-1/2 top-5 h-1 w-full translate-x-1/2 rounded-full"
                style={{
                  background:
                    stages[i - 1].state === "complete"
                      ? "linear-gradient(90deg, rgba(158,212,181,0.5), rgba(196,181,232,0.3))"
                      : "rgba(58,53,80,0.8)",
                }}
              />
            )}
            <motion.div
              className={`relative z-10 flex h-11 w-11 items-center justify-center rounded-2xl text-lg ${
                stage.state === "complete"
                  ? "bg-cozy-sage/25 shadow-cozy"
                  : stage.state === "current"
                    ? "bg-cozy-peach/25 ring-2 ring-cozy-peach/40"
                    : stage.state === "failed"
                      ? "bg-cozy-rose/20"
                      : "bg-cozy-elevated/80 opacity-70"
              }`}
              animate={stage.state === "current" ? { scale: [1, 1.08, 1] } : {}}
              transition={{ repeat: Infinity, duration: 2.2 }}
            >
              {emoji}
            </motion.div>
            <span
              className={`mt-1.5 text-[9px] font-semibold ${
                stage.state === "current" ? "text-cozy-peach" : "text-cozy-muted"
              }`}
            >
              {label}
            </span>
          </div>
        );
      })}
    </div>
  );
}
