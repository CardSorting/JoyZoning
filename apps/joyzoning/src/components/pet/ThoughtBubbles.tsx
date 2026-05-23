"use client";

import { motion, AnimatePresence } from "framer-motion";

export function ThoughtBubbles({ thoughts }: { thoughts: string[] }) {
  return (
    <div className="space-y-2">
      <p className="text-xs font-semibold uppercase tracking-wide text-pet-muted">
        Recent thoughts
      </p>
      <ul className="space-y-2">
        <AnimatePresence initial={false}>
          {thoughts.map((text, i) => (
            <motion.li
              key={`${i}-${text.slice(0, 24)}`}
              initial={{ opacity: 0, x: -8 }}
              animate={{ opacity: 1, x: 0 }}
              exit={{ opacity: 0 }}
              className="relative rounded-2xl rounded-bl-sm bg-pet-elevated/90 px-3 py-2 text-sm text-pet-cream"
            >
              <span className="absolute -left-1 bottom-2 h-2 w-2 rotate-45 bg-pet-elevated/90" aria-hidden />
              {text}
            </motion.li>
          ))}
        </AnimatePresence>
      </ul>
    </div>
  );
}
