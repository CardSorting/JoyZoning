"use client";

import { motion } from "framer-motion";

function GrowthBar({
  label,
  value,
  emoji,
  gradient,
}: {
  label: string;
  value: number;
  emoji: string;
  gradient: string;
}) {
  return (
    <div className="space-y-1.5">
      <div className="flex justify-between text-xs">
        <span className="text-cozy-muted">
          {emoji} {label}
        </span>
        <span className="font-medium text-cozy-cream">{value}%</span>
      </div>
      <div className="h-2.5 overflow-hidden rounded-full bg-cozy-deep/80">
        <motion.div
          className={`h-full rounded-full ${gradient}`}
          initial={{ width: 0 }}
          animate={{ width: `${value}%` }}
          transition={{ type: "spring", stiffness: 50, damping: 20 }}
        />
      </div>
    </div>
  );
}

export function MomentumBars({
  momentum,
  convergence,
  harmony,
  confidence,
  flow,
  synthesisCharge,
}: {
  momentum: number;
  convergence: number;
  harmony: number;
  confidence: number;
  flow: number;
  synthesisCharge: number;
}) {
  return (
    <div className="grid gap-3 sm:grid-cols-2">
      <GrowthBar label="Momentum" value={momentum} emoji="🌿" gradient="bg-gradient-to-r from-cozy-sage/80 to-cozy-sky/60" />
      <GrowthBar label="Convergence" value={convergence} emoji="🌈" gradient="bg-gradient-to-r from-cozy-lavender/70 to-cozy-sage/60" />
      <GrowthBar label="Harmony" value={harmony} emoji="☯" gradient="bg-gradient-to-r from-cozy-sage/70 to-cozy-peach/50" />
      <GrowthBar label="Confidence" value={confidence} emoji="💛" gradient="bg-gradient-to-r from-cozy-gold/70 to-cozy-peach/50" />
      <GrowthBar label="Flow" value={flow} emoji="💧" gradient="bg-gradient-to-r from-cozy-sky/60 to-cozy-lavender/50" />
      <GrowthBar label="Synthesis warmth" value={synthesisCharge} emoji="✨" gradient="bg-gradient-to-r from-cozy-peach/60 via-cozy-gold/50 to-cozy-sage/50" />
    </div>
  );
}
