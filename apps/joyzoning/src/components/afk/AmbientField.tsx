"use client";

import { motion } from "framer-motion";
import type { WorldWeather } from "@/lib/joyzone";
import { useReducedMotion } from "@/lib/useReducedMotion";

const SKY: Record<WorldWeather, string> = {
  dawn: "radial-gradient(ellipse at 50% 0%, rgba(244,201,168,0.2), rgba(168,212,240,0.08) 45%, transparent 70%)",
  rain: "radial-gradient(ellipse at 50% 20%, rgba(168,212,240,0.18), transparent 60%)",
  aurora: "radial-gradient(ellipse at 50% 0%, rgba(158,212,181,0.2), rgba(196,181,232,0.1) 50%, transparent 75%)",
  wind: "radial-gradient(ellipse at 80% 30%, rgba(158,212,181,0.12), transparent 55%)",
  eclipse: "radial-gradient(ellipse at 50% 40%, rgba(196,181,232,0.15), rgba(26,22,40,0.4) 80%)",
  bloom: "radial-gradient(ellipse at 50% 60%, rgba(240,215,140,0.2), rgba(158,212,181,0.15) 50%, transparent 75%)",
  night: "radial-gradient(ellipse at 50% 0%, rgba(196,181,232,0.08), transparent 50%)",
};

export function AmbientField({
  weather,
  particleCount = 24,
}: {
  weather: WorldWeather;
  particleCount?: number;
}) {
  const reduced = useReducedMotion();

  return (
    <div className="pointer-events-none fixed inset-0 overflow-hidden" aria-hidden>
      <div
        className="absolute inset-0 transition-all duration-[3s]"
        style={{ background: SKY[weather] }}
      />

      {weather === "rain" && !reduced && (
        <div className="absolute inset-0 opacity-40">
          {Array.from({ length: 18 }).map((_, i) => (
            <motion.span
              key={i}
              className="absolute h-8 w-0.5 rounded-full bg-cozy-sky/30"
              style={{ left: `${(i * 11) % 100}%`, top: "-10%" }}
              animate={{ y: ["0vh", "110vh"] }}
              transition={{
                repeat: Infinity,
                duration: 1.2 + (i % 5) * 0.15,
                delay: i * 0.08,
                ease: "linear",
              }}
            />
          ))}
        </div>
      )}

      {weather === "night" && (
        <div className="absolute inset-0">
          {Array.from({ length: reduced ? 8 : 20 }).map((_, i) => (
            <motion.span
              key={i}
              className="absolute h-1 w-1 rounded-full bg-cozy-cream/60"
              style={{ left: `${(i * 19) % 100}%`, top: `${(i * 13) % 60}%` }}
              animate={reduced ? {} : { opacity: [0.3, 0.9, 0.3] }}
              transition={{ repeat: Infinity, duration: 2 + (i % 4), delay: i * 0.1 }}
            />
          ))}
        </div>
      )}

      {weather === "bloom" && !reduced && (
        <div className="absolute inset-0">
          {Array.from({ length: 10 }).map((_, i) => (
            <motion.span
              key={i}
              className="absolute text-sm opacity-60"
              style={{ left: `${10 + i * 9}%` }}
              animate={{ y: [0, 80, 120], opacity: [0, 0.7, 0], rotate: [0, 180] }}
              transition={{
                repeat: Infinity,
                duration: 6 + i,
                delay: i * 0.5,
              }}
            >
              🌸
            </motion.span>
          ))}
        </div>
      )}

      {weather === "aurora" && !reduced && (
        <motion.div
          className="absolute inset-x-0 top-0 h-1/3 opacity-30"
          animate={{ opacity: [0.15, 0.35, 0.2] }}
          transition={{ repeat: Infinity, duration: 5 }}
          style={{
            background:
              "linear-gradient(180deg, rgba(158,212,181,0.25), rgba(196,181,232,0.12), transparent)",
          }}
        />
      )}

      {!reduced &&
        Array.from({ length: particleCount }).map((_, i) => (
          <motion.span
            key={i}
            className="absolute rounded-full bg-cozy-sage/50"
            style={{
              left: `${(i * 17 + 5) % 100}%`,
              top: `${(i * 23 + 9) % 100}%`,
              width: 3 + (i % 2),
              height: 3 + (i % 2),
            }}
            animate={{ y: [0, -20, 0], opacity: [0.15, 0.5, 0.15] }}
            transition={{
              repeat: Infinity,
              duration: 7 + (i % 8),
              delay: (i % 6) * 0.3,
            }}
          />
        ))}
    </div>
  );
}
