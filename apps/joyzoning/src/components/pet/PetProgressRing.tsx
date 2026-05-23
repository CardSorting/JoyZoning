"use client";

import { motion } from "framer-motion";

export function PetProgressRing({ percent, phase }: { percent: number; phase: string }) {
  const r = 44;
  const c = 2 * Math.PI * r;
  const offset = c - (percent / 100) * c;

  return (
    <div className="flex items-center gap-4">
      <div className="relative h-[100px] w-[100px] shrink-0">
        <svg className="-rotate-90" viewBox="0 0 100 100" aria-hidden>
          <circle
            cx="50"
            cy="50"
            r={r}
            fill="none"
            stroke="currentColor"
            strokeWidth="8"
            className="text-pet-deep"
          />
          <motion.circle
            cx="50"
            cy="50"
            r={r}
            fill="none"
            stroke="url(#petRing)"
            strokeWidth="8"
            strokeLinecap="round"
            strokeDasharray={c}
            initial={{ strokeDashoffset: c }}
            animate={{ strokeDashoffset: offset }}
            transition={{ duration: 0.55, ease: "easeOut" }}
          />
          <defs>
            <linearGradient id="petRing" x1="0%" y1="0%" x2="100%" y2="0%">
              <stop offset="0%" stopColor="#9ed4b5" />
              <stop offset="100%" stopColor="#f0d78c" />
            </linearGradient>
          </defs>
        </svg>
        <span className="absolute inset-0 flex items-center justify-center text-lg font-bold text-pet-cream">
          {Math.round(percent)}%
        </span>
      </div>
      <div className="min-w-0 flex-1">
        <p className="text-xs font-semibold uppercase tracking-wide text-pet-muted">
          Current phase
        </p>
        <p className="mt-0.5 truncate text-base font-semibold text-pet-cream">{phase}</p>
      </div>
    </div>
  );
}
