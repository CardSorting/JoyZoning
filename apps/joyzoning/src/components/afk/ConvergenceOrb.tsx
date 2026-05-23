"use client";

import { motion } from "framer-motion";
import { useReducedMotion } from "@/lib/useReducedMotion";

export function ConvergenceOrb({
  convergence,
  momentum,
  confidence,
  phaseWhisper,
  intentEcho,
  isActive,
  stabilizing,
  blooming,
}: {
  convergence: number;
  momentum: number;
  confidence: number;
  phaseWhisper: string;
  intentEcho: string;
  isActive: boolean;
  stabilizing: boolean;
  blooming: boolean;
}) {
  const reduced = useReducedMotion();
  const r = 84;
  const c = 2 * Math.PI * r;
  const offset = c - (convergence / 100) * c;
  const dim = confidence < 40;

  return (
    <div className="relative flex flex-col items-center">
      <p className="mb-3 text-[10px] font-semibold uppercase tracking-[0.25em] text-cozy-peach/90">
        Heart of the JoyZone
      </p>

      {!reduced && (isActive || stabilizing) && (
        <motion.div
          className={`absolute rounded-full ${blooming ? "bg-cozy-gold/15" : "bg-cozy-sage/10"}`}
          style={{ width: 200, height: 200 }}
          animate={
            isActive
              ? { scale: [1, 1.12, 1], opacity: [0.4, 0.7, 0.4] }
              : { scale: [1, 1.06, 1], opacity: [0.3, 0.5, 0.3] }
          }
          transition={{ repeat: Infinity, duration: 3.8, ease: "easeInOut" }}
          aria-hidden
        />
      )}

      <div className="relative h-[196px] w-[196px] sm:h-[210px] sm:w-[210px]">
        <svg className="-rotate-90" viewBox="0 0 200 200" aria-hidden>
          <circle
            cx="100"
            cy="100"
            r={r}
            fill="none"
            stroke="rgba(196,181,232,0.15)"
            strokeWidth="6"
          />
          <motion.circle
            cx="100"
            cy="100"
            r={r}
            fill="none"
            stroke="url(#heartGrad)"
            strokeWidth="7"
            strokeLinecap="round"
            strokeDasharray={c}
            animate={{ strokeDashoffset: offset, opacity: dim ? 0.5 : 1 }}
            transition={{ type: "spring", stiffness: 45, damping: 22 }}
          />
          <defs>
            <linearGradient id="heartGrad" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stopColor="#9ed4b5" />
              <stop offset="50%" stopColor="#c4b5e8" />
              <stop offset="100%" stopColor="#f4c9a8" />
            </linearGradient>
          </defs>
        </svg>

        <div className="absolute inset-0 flex flex-col items-center justify-center px-6 text-center">
          <motion.span
            className="text-3xl font-bold text-cozy-sage sm:text-4xl"
            animate={blooming && !reduced ? { scale: [1, 1.05, 1] } : {}}
            transition={{ repeat: Infinity, duration: 2 }}
          >
            {convergence}%
          </motion.span>
          <span className="mt-0.5 text-[10px] font-medium text-cozy-muted">Harmony</span>
          <div className="mt-2 flex gap-3 text-[10px] text-cozy-muted">
            <span>🌿 {momentum}</span>
            <span>💛 {confidence}</span>
          </div>
          {stabilizing && (
            <span className="mt-2 rounded-full bg-cozy-sage/20 px-2.5 py-0.5 text-[9px] font-semibold text-cozy-sage">
              Stabilizing…
            </span>
          )}
        </div>

        {blooming && !reduced && (
          <motion.span
            className="absolute -top-1 right-4 text-lg"
            animate={{ y: [0, -6, 0], rotate: [0, 10, 0] }}
            transition={{ repeat: Infinity, duration: 2.5 }}
          >
            🌸
          </motion.span>
        )}
      </div>

      <h2 className="mt-3 max-w-xs text-center text-lg font-semibold leading-snug text-cozy-cream">
        {intentEcho}
      </h2>
      <p className="mt-2 max-w-sm text-center text-sm leading-relaxed text-cozy-muted">
        {phaseWhisper}
      </p>
    </div>
  );
}
