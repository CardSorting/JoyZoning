"use client";

import { motion, AnimatePresence } from "framer-motion";
import { useReducedMotion } from "@/lib/useReducedMotion";

export function CelebrationLayer({
  show,
  message,
}: {
  show: boolean;
  message: string;
}) {
  const reduced = useReducedMotion();

  return (
    <AnimatePresence>
      {show && (
        <motion.div
          className="pointer-events-none fixed inset-0 z-50 flex items-center justify-center"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          role="status"
        >
          {!reduced &&
            ["🌸", "✨", "🌿", "🦋"].map((e, i) => (
              <motion.span
                key={i}
                className="absolute text-2xl"
                initial={{ scale: 0, opacity: 0 }}
                animate={{
                  scale: [0, 1.1, 0],
                  opacity: [0, 1, 0],
                  y: [20, -60 - i * 15],
                  x: (i - 1.5) * 40,
                }}
                transition={{ duration: 1.4, delay: i * 0.08 }}
              >
                {e}
              </motion.span>
            ))}
          <motion.p
            className="rounded-cozy-lg border border-cozy-sage/30 bg-cozy-surface/95 px-6 py-3 text-center text-base font-semibold text-cozy-sage shadow-cozy backdrop-blur-md"
            initial={{ scale: 0.92, y: 10 }}
            animate={{ scale: 1, y: 0 }}
            exit={{ opacity: 0 }}
          >
            {message}
          </motion.p>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
