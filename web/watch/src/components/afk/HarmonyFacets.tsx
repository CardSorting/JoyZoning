"use client";

import { motion } from "framer-motion";
import type { Deliverable } from "@/lib/types";

const FACET_NAMES: Record<string, { name: string; emoji: string }> = {
  package: { name: "Village foundation", emoji: "🏡" },
  screens: { name: "Window blooms", emoji: "🪟" },
  features: { name: "Garden beds", emoji: "🌿" },
  shared: { name: "Shared grove", emoji: "🌳" },
  readme: { name: "Story stone", emoji: "📜" },
};

export function HarmonyFacets({ items }: { items: Deliverable[] }) {
  if (!items.length) {
    return (
      <div className="flex flex-col items-center py-10 text-center text-cozy-muted">
        <span className="text-4xl opacity-50">🌱</span>
        <p className="mt-3 text-sm">Seeds are still underground. Patience is part of the game.</p>
      </div>
    );
  }

  return (
    <ul className="grid gap-2 sm:grid-cols-2">
      {items.map((d, i) => {
        const meta = FACET_NAMES[d.id] ?? { name: d.label, emoji: "✨" };
        return (
          <motion.li
            key={d.id}
            initial={{ opacity: 0, y: 6 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: i * 0.05 }}
            className={`cozy-card flex items-center gap-3 p-3 ${
              d.done ? "border-cozy-sage/35 bg-cozy-sage/10" : ""
            }`}
          >
            <span className="text-2xl">{d.done ? "🌸" : meta.emoji}</span>
            <div>
              <p className="font-medium text-cozy-cream">{meta.name}</p>
              {d.count != null && d.count > 0 && (
                <p className="text-xs text-cozy-muted">{d.count} sprouts</p>
              )}
              {!d.done && <p className="text-xs text-cozy-sage/80">Growing…</p>}
            </div>
          </motion.li>
        );
      })}
    </ul>
  );
}
