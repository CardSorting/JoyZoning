"use client";

import { motion, AnimatePresence } from "framer-motion";
import { useReducedMotion } from "@/lib/useReducedMotion";

export function MemoryWhispers({ whispers }: { whispers: string[] }) {
  const reduced = useReducedMotion();
  const visible = whispers.slice(-5);

  return (
    <ul className="min-h-[5rem] space-y-1" aria-live="polite" aria-label="Habitat whispers">
      <AnimatePresence mode="popLayout">
        {visible.length === 0 ? (
          <motion.li
            key="empty"
            className="py-6 text-center text-sm italic text-cozy-muted/80"
          >
            The village is quiet… whispers will drift in soon.
          </motion.li>
        ) : (
          visible.map((text, i) => (
            <motion.li
              key={`${text}-${i}`}
              layout={!reduced}
              initial={reduced ? {} : { opacity: 0, x: -8 }}
              animate={{ opacity: 1 - i * 0.1, x: 0 }}
              exit={{ opacity: 0 }}
              className="flex items-start gap-2 rounded-xl bg-cozy-deep/40 px-3 py-2"
            >
              <span className="text-sm">💬</span>
              <span className="text-sm leading-snug text-cozy-cream/90">{text}</span>
            </motion.li>
          ))
        )}
      </AnimatePresence>
    </ul>
  );
}
