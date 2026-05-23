"use client";

import { motion } from "framer-motion";
import { useReducedMotion } from "@/lib/useReducedMotion";

export function PetHeader({
  statusLine,
  variant,
}: {
  statusLine: string;
  variant: "live" | "error" | "idle";
}) {
  const reduced = useReducedMotion();
  const dot =
    variant === "live"
      ? "bg-pet-mint"
      : variant === "error"
        ? "bg-pet-rose"
        : "bg-pet-gold/80";

  return (
    <header className="sticky top-0 z-30 border-b border-pet-elevated/60 bg-pet-night/92 backdrop-blur-md">
      <div className="mx-auto flex max-w-2xl items-center justify-between gap-3 px-4 py-3 sm:max-w-3xl">
        <div className="flex items-center gap-2.5">
          <motion.span
            className="text-xl font-bold text-pet-mint"
            animate={reduced ? {} : { scale: [1, 1.06, 1] }}
            transition={{ repeat: Infinity, duration: 3 }}
            aria-hidden
          >
            ◈
          </motion.span>
          <div>
            <p className="text-[10px] font-semibold uppercase tracking-wider text-pet-mint">
              JoyZone Watch
            </p>
            <h1 className="text-base font-bold text-pet-cream">Synthesis Pet</h1>
          </div>
        </div>
        <div className="pet-card flex items-center gap-2 px-3 py-1.5">
          <span className={`h-2 w-2 rounded-full ${dot}`} aria-hidden />
          <span className="max-w-[160px] truncate text-xs text-pet-muted sm:max-w-none">
            {statusLine}
          </span>
        </div>
      </div>
    </header>
  );
}
