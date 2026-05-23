"use client";

import { useState } from "react";
import { motion, AnimatePresence } from "framer-motion";
import {
  VILLAGE_BIOMES,
  INTENT_SEEDS,
  seedPreview,
  type VillageBiome,
  type IntentSeedKind,
} from "@/lib/joyzone";
import { savePrefs } from "@/lib/session-prefs";
import { useReducedMotion } from "@/lib/useReducedMotion";
import { AmbientField } from "./AmbientField";
import type { WorldWeather } from "@/lib/joyzone";
import type { ActiveTaskSummary, WatchSession } from "@/lib/types";

type Step = "welcome" | "biome" | "seed" | "plant" | "ready";

function biomePreviewWeather(b: VillageBiome): WorldWeather {
  if (b === "rain") return "rain";
  if (b === "bloom") return "bloom";
  if (b === "eclipse") return "eclipse";
  if (b === "dawn") return "dawn";
  return "aurora";
}

export function EntryFlow({
  sessions,
  activeTasks,
  sessionId,
  taskId,
  taskOptions,
  initialBiome,
  initialSeed,
  onSessionChange,
  onTaskChange,
  onEnter,
}: {
  sessions: WatchSession[];
  activeTasks: ActiveTaskSummary[];
  sessionId: string;
  taskId: string;
  taskOptions: { id: string; title: string; status: number }[];
  initialBiome: VillageBiome;
  initialSeed: IntentSeedKind;
  onSessionChange: (id: string) => void;
  onTaskChange: (id: string) => void;
  onEnter: (biome: VillageBiome, seed: IntentSeedKind) => void;
}) {
  const reduced = useReducedMotion();
  const [step, setStep] = useState<Step>("welcome");
  const [biome, setBiome] = useState<VillageBiome>(initialBiome);
  const [seed, setSeed] = useState<IntentSeedKind>(initialSeed);

  const begin = () => {
    savePrefs(biome, seed);
    onEnter(biome, seed);
  };

  return (
    <div className="relative min-h-[75vh] pb-8">
      <AmbientField
        weather={biomePreviewWeather(biome)}
        particleCount={reduced ? 10 : 28}
      />

      <div className="relative z-10 mx-auto max-w-lg px-1">
        <AnimatePresence mode="wait">
          {step === "welcome" && (
            <motion.div
              key="welcome"
              initial={{ opacity: 0, y: 12 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0 }}
              className="text-center"
            >
              <motion.p
                className="text-5xl"
                animate={reduced ? {} : { rotate: [0, 4, -4, 0] }}
                transition={{ repeat: Infinity, duration: 5 }}
              >
                🏡
              </motion.p>
              <h1 className="mt-4 text-3xl font-bold text-cozy-cream sm:text-4xl">
                Enter the JoyZone
              </h1>
              <p className="mx-auto mt-3 max-w-sm text-sm leading-relaxed text-cozy-muted">
                A cozy village where software grows while you watch. No code. No terminals.
                Just a living habitat to care for.
              </p>
              <button
                type="button"
                onClick={() => setStep("biome")}
                className="mt-10 w-full rounded-cozy-lg bg-gradient-to-r from-cozy-sage/90 via-cozy-lavender/80 to-cozy-peach/80 py-4 text-sm font-bold text-cozy-night shadow-cozy"
              >
                Visit the village
              </button>
            </motion.div>
          )}

          {step === "biome" && (
            <motion.div key="biome" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}>
              <h2 className="text-center text-xl font-bold text-cozy-cream">
                Choose your home biome
              </h2>
              <p className="mt-2 text-center text-sm text-cozy-muted">
                This colors the sky and mood of your sanctuary.
              </p>
              <ul className="mt-6 grid gap-2">
                {VILLAGE_BIOMES.map((b) => (
                  <li key={b.id}>
                    <button
                      type="button"
                      onClick={() => setBiome(b.id)}
                      className={`cozy-card flex w-full items-center gap-4 p-4 text-left ${
                        biome === b.id ? "border-cozy-sage/50 bg-cozy-sage/10" : ""
                      }`}
                    >
                      <span className="text-3xl">{b.icon}</span>
                      <div>
                        <p className="font-semibold text-cozy-cream">{b.label}</p>
                        <p className="text-xs text-cozy-muted">{b.tagline}</p>
                      </div>
                    </button>
                  </li>
                ))}
              </ul>
              <NavButtons onBack={() => setStep("welcome")} onNext={() => setStep("seed")} />
            </motion.div>
          )}

          {step === "seed" && (
            <motion.div key="seed" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}>
              <h2 className="text-center text-xl font-bold text-cozy-cream">
                Plant an intent seed
              </h2>
              <p className="mt-2 text-center text-sm text-cozy-muted">
                How should the village interpret your wish?
              </p>
              <ul className="mt-6 space-y-2">
                {INTENT_SEEDS.map((s) => (
                  <li key={s.id}>
                    <button
                      type="button"
                      onClick={() => setSeed(s.id)}
                      className={`cozy-card flex w-full items-center gap-3 p-4 ${
                        seed === s.id ? "border-cozy-peach/50 bg-cozy-peach/10" : ""
                      }`}
                    >
                      <span className="text-2xl">{s.icon}</span>
                      <div className="text-left">
                        <p className="font-semibold text-cozy-cream">{s.seedName}</p>
                        <p className="text-xs text-cozy-muted">{s.promise}</p>
                      </div>
                    </button>
                  </li>
                ))}
              </ul>
              <NavButtons onBack={() => setStep("biome")} onNext={() => setStep("plant")} />
            </motion.div>
          )}

          {step === "plant" && (
            <motion.div key="plant" initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="cozy-panel space-y-4 p-5">
              <h2 className="text-lg font-bold text-cozy-cream">Where to plant?</h2>
              <label className="block">
                <span className="text-xs font-medium text-cozy-muted">Village habitat</span>
                <select
                  className="mt-1 w-full rounded-cozy border border-cozy-elevated bg-cozy-deep px-4 py-3 text-cozy-cream"
                  value={sessionId}
                  onChange={(e) => onSessionChange(e.target.value)}
                >
                  <option value="">Choose…</option>
                  {sessions.map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.name || "Your village"}
                    </option>
                  ))}
                </select>
              </label>
              <label className="block">
                <span className="text-xs font-medium text-cozy-muted">Active synthesis</span>
                <select
                  className="mt-1 w-full rounded-cozy border border-cozy-elevated bg-cozy-deep px-4 py-3 text-cozy-cream"
                  value={taskId}
                  onChange={(e) => onTaskChange(e.target.value)}
                  disabled={!sessionId}
                >
                  <option value="">Choose…</option>
                  {taskOptions.map((t) => (
                    <option key={t.id} value={t.id}>
                      {(t.title || "Seed").slice(0, 50)}
                    </option>
                  ))}
                </select>
              </label>
              {activeTasks.length > 0 && (
                <div className="flex flex-wrap gap-2">
                  {activeTasks.slice(0, 4).map((t) => (
                    <button
                      key={t.taskId}
                      type="button"
                      onClick={() => {
                        onSessionChange(t.sessionId);
                        onTaskChange(t.taskId);
                      }}
                      className="rounded-full bg-cozy-sage/15 px-3 py-1 text-xs text-cozy-sage"
                    >
                      🌻 live plot
                    </button>
                  ))}
                </div>
              )}
              <NavButtons
                onBack={() => setStep("seed")}
                onNext={() => setStep("ready")}
                nextDisabled={!taskId}
                nextLabel="Almost there"
              />
            </motion.div>
          )}

          {step === "ready" && (
            <motion.div key="ready" className="cozy-panel p-6 text-center">
              <p className="text-4xl">🌱✨</p>
              <h2 className="mt-4 text-xl font-bold text-cozy-cream">Ready to tend</h2>
              <p className="mt-3 text-sm leading-relaxed text-cozy-muted">
                {seedPreview(seed)}
              </p>
              <p className="mt-4 text-xs text-cozy-sage">
                {VILLAGE_BIOMES.find((b) => b.id === biome)?.icon}{" "}
                {VILLAGE_BIOMES.find((b) => b.id === biome)?.label}
              </p>
              <button
                type="button"
                disabled={!taskId}
                onClick={begin}
                className="mt-8 w-full rounded-cozy-lg bg-cozy-sage/90 py-4 text-sm font-bold text-cozy-night shadow-cozy disabled:opacity-40"
              >
                Settle into the habitat
              </button>
              <button
                type="button"
                onClick={() => setStep("plant")}
                className="mt-2 w-full py-2 text-xs text-cozy-muted"
              >
                Change planting
              </button>
            </motion.div>
          )}
        </AnimatePresence>
      </div>
    </div>
  );
}

function NavButtons({
  onBack,
  onNext,
  nextDisabled,
  nextLabel = "Continue",
}: {
  onBack: () => void;
  onNext: () => void;
  nextDisabled?: boolean;
  nextLabel?: string;
}) {
  return (
    <div className="mt-6 flex gap-2">
      <button type="button" onClick={onBack} className="flex-1 py-3 text-sm text-cozy-muted">
        Back
      </button>
      <button
        type="button"
        disabled={nextDisabled}
        onClick={onNext}
        className="flex-[2] rounded-cozy bg-cozy-elevated py-3 text-sm font-semibold text-cozy-sage disabled:opacity-40"
      >
        {nextLabel}
      </button>
    </div>
  );
}
