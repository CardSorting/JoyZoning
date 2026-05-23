"use client";

import { motion } from "framer-motion";
import { useReducedMotion } from "@/lib/useReducedMotion";

export function AfkHeader({
  resonance,
  variant,
}: {
  resonance: string;
  variant: "live" | "error" | "idle";
}) {
  const reduced = useReducedMotion();
  const dot =
    variant === "live"
      ? "bg-cozy-sage"
      : variant === "error"
        ? "bg-cozy-rose"
        : "bg-cozy-gold/80";

  return (
    <header className="sticky top-0 z-30 border-b border-cozy-elevated/60 bg-cozy-night/90 backdrop-blur-md">
      <div className="mx-auto flex max-w-2xl items-center justify-between gap-3 px-4 py-3 sm:max-w-3xl">
        <div className="flex items-center gap-2.5">
          <motion.span
            className="text-2xl"
            animate={reduced ? {} : { rotate: [0, 5, -5, 0] }}
            transition={{ repeat: Infinity, duration: 6 }}
          >
            🏡
          </motion.span>
          <div>
            <p className="text-[10px] font-semibold uppercase tracking-wider text-cozy-sage">
              JoyZone
            </p>
            <h1 className="text-base font-bold text-cozy-cream">Cozy Synthesis Village</h1>
          </div>
        </div>
        <div className="cozy-card flex items-center gap-2 px-3 py-1.5">
          <span className={`h-2 w-2 rounded-full ${dot}`} aria-hidden />
          <span className="max-w-[140px] truncate text-xs text-cozy-muted sm:max-w-none">
            {resonance}
          </span>
        </div>
      </div>
    </header>
  );
}
