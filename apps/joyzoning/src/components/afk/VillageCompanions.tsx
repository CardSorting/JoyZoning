"use client";

import { motion } from "framer-motion";
import { useMemo } from "react";
import type { CompanionMode } from "@/lib/joyzone";
import { useReducedMotion } from "@/lib/useReducedMotion";

const SPIRITS = ["🌱", "✨", "🐚", "🍃", "🦋", "🌸", "💫", "🪻"];

export function VillageCompanions({
  count,
  mode,
}: {
  count: number;
  mode: CompanionMode;
}) {
  const reduced = useReducedMotion();

  const companions = useMemo(() => {
    const hubX = 48;
    const hubY = 45;
    return Array.from({ length: count }, (_, i) => {
      const seed = i * 1.618;
      let pathX: number[];
      let pathY: number[];

      if (mode === "confused") {
        pathX = [20 + seed * 10, 75 - seed * 4, 15, 80, 50];
        pathY = [70, 25, 75, 35, 50];
      } else if (mode === "celebrating") {
        pathX = [hubX - 8, hubX, hubX + 8, hubX, hubX - 4];
        pathY = [hubY - 10, hubY - 14, hubY - 8, hubY, hubY - 6];
      } else if (mode === "tending") {
        pathX = [
          hubX - 12 + (i % 3) * 8,
          hubX + (i % 2) * 6,
          hubX - 4 + (i % 4) * 3,
          hubX,
        ];
        pathY = [hubY + (i % 3) * 5, hubY - 6, hubY + 8, hubY + 2];
      } else if (mode === "resting") {
        pathX = [30 + i * 8, 32 + i * 8, 31 + i * 8];
        pathY = [65, 66, 65];
      } else {
        pathX = [8 + seed * 7, 28 + seed * 4, 52 + seed * 2, 72, 22 + seed * 3];
        pathY = [58 + (i % 4) * 5, 42 + (i % 3) * 7, 52, 38 + (i % 5) * 4, 60];
      }

      return {
        id: i,
        emoji: SPIRITS[i % SPIRITS.length],
        pathX,
        pathY,
        duration: mode === "celebrating" ? 2.5 : mode === "tending" ? 7 : 14 + (i % 6),
      };
    });
  }, [count, mode]);

  const modeLabel =
    mode === "tending"
      ? "Tending your intent"
      : mode === "celebrating"
        ? "Celebrating harmony"
        : mode === "confused"
          ? "A little puzzled"
          : mode === "resting"
            ? "Resting peacefully"
            : "Wandering the village";

  return (
    <div className="relative h-36 w-full sm:h-40" aria-label={modeLabel} role="img">
      <p className="mb-2 text-center text-[10px] font-medium text-cozy-muted">
        {modeLabel}
      </p>
      {companions.map((c) => (
        <motion.div
          key={c.id}
          className="absolute text-xl sm:text-2xl"
          style={{ filter: "drop-shadow(0 2px 6px rgba(0,0,0,0.2))" }}
          initial={{ left: `${c.pathX[0]}%`, top: `${c.pathY[0]}%` }}
          animate={
            reduced
              ? { opacity: [0.6, 0.9, 0.6] }
              : {
                  left: c.pathX.map((x) => `${x}%`),
                  top: c.pathY.map((y) => `${y}%`),
                  scale: mode === "celebrating" ? [1, 1.2, 1] : [1, 1.05, 1],
                }
          }
          transition={{
            repeat: Infinity,
            duration: c.duration,
            ease: "easeInOut",
          }}
        >
          {c.emoji}
        </motion.div>
      ))}
    </div>
  );
}
