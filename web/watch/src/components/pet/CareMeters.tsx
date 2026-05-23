"use client";

import { motion } from "framer-motion";
import type { CareMeters as Meters } from "@/lib/vitals";

const ROWS: { key: keyof Meters; label: string; icon: string; color: string }[] = [
  { key: "clarity", label: "Clarity", icon: "💡", color: "bg-pet-sky" },
  { key: "energy", label: "Energy", icon: "⚡", color: "bg-pet-gold" },
  { key: "confidence", label: "Confidence", icon: "🛡️", color: "bg-pet-mint" },
];

export function CareMeters({ meters }: { meters: Meters }) {
  return (
    <div className="space-y-2.5">
      {ROWS.map(({ key, label, icon, color }) => (
        <div key={key}>
          <div className="mb-1 flex items-center justify-between text-xs">
            <span className="text-pet-muted">
              {icon} {label}
            </span>
            <span className="font-mono text-pet-cream">{meters[key]}%</span>
          </div>
          <div className="h-2 overflow-hidden rounded-full bg-pet-deep/80">
            <motion.div
              className={`h-full rounded-full ${color}`}
              initial={{ width: 0 }}
              animate={{ width: `${meters[key]}%` }}
              transition={{ duration: 0.5, ease: "easeOut" }}
            />
          </div>
        </div>
      ))}
    </div>
  );
}
