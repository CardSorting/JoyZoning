"use client";

import { motion } from "framer-motion";
import { MOOD_META, type PetMood } from "@/lib/pet";
import { useReducedMotion } from "@/lib/useReducedMotion";
import { cn } from "@/lib/cn";

const BODY: Record<PetMood, string> = {
  calm: "from-pet-shell via-pet-cream to-pet-sky/40",
  focused: "from-pet-mint/80 via-pet-shell to-pet-cream",
  excited: "from-pet-gold/70 via-pet-shell to-pet-mint/50",
  confused: "from-pet-lavender/60 via-pet-shell to-pet-cream",
  sick: "from-pet-rose/50 via-pet-shell to-pet-muted/30",
  tired: "from-pet-muted/40 via-pet-shell to-pet-deep",
  happy: "from-pet-mint via-pet-gold/40 to-pet-shell",
  panicking: "from-pet-rose via-pet-gold/30 to-pet-shell",
};

const EYE: Record<PetMood, string> = {
  calm: "scale-y-75",
  focused: "scale-y-110",
  excited: "scale-125",
  confused: "rotate-12",
  sick: "opacity-70",
  tired: "scale-y-50 opacity-60",
  happy: "scale-110",
  panicking: "animate-pulse",
};

export function SynthesisPet({
  mood,
  needsYou,
}: {
  mood: PetMood;
  needsYou: boolean;
}) {
  const reduced = useReducedMotion();
  const meta = MOOD_META[mood];

  return (
    <div className="relative flex flex-col items-center">
      {needsYou && (
        <motion.span
          className="absolute -top-1 right-6 rounded-full bg-pet-rose px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide text-pet-night"
          animate={reduced ? {} : { y: [0, -3, 0] }}
          transition={{ repeat: Infinity, duration: 1.2 }}
        >
          needs you
        </motion.span>
      )}

      <motion.div
        className={cn(
          "relative flex h-44 w-44 items-center justify-center rounded-[45%] bg-gradient-to-br shadow-pet",
          BODY[mood],
          !reduced && mood !== "tired" && mood !== "sick" && "animate-pet-bob",
        )}
        aria-hidden
      >
        <div
          className={cn(
            "absolute inset-3 rounded-[42%] border-2 border-pet-night/10 bg-pet-cream/20",
          )}
        />
        <div className="absolute top-[38%] flex w-[55%] justify-between px-2">
          <span
            className={cn(
              "h-3 w-3 rounded-full bg-pet-night shadow-inner",
              EYE[mood],
            )}
          />
          <span
            className={cn(
              "h-3 w-3 rounded-full bg-pet-night shadow-inner",
              EYE[mood],
            )}
          />
        </div>
        <div
          className={cn(
            "absolute bottom-[32%] h-2 rounded-full bg-pet-night/70",
            mood === "happy" ? "w-8" : mood === "sick" || mood === "tired" ? "w-4" : "w-6",
          )}
        />
        {(mood === "excited" || mood === "panicking") && !reduced && (
          <>
            <span className="absolute -left-1 top-8 text-lg">✦</span>
            <span className="absolute -right-1 top-10 text-sm">✦</span>
          </>
        )}
      </motion.div>

      <p className={cn("mt-3 text-sm font-semibold", meta.color)}>
        {meta.emoji} {meta.label}
      </p>
      <p className="mt-0.5 max-w-[240px] text-center text-xs text-pet-muted">
        {meta.hint}
      </p>
    </div>
  );
}
