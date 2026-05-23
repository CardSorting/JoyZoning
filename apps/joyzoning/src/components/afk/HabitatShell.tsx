"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { motion } from "framer-motion";
import { buildPipelineStages, type BoardTask } from "@/lib/kanban";
import {
  INTENT_SEEDS,
  VILLAGE_BIOMES,
  companionModeFromVitals,
  type VillageBiome,
  type SanctuaryTab,
  type IntentSeedKind,
} from "@/lib/joyzone";
import { computeVitals, buildMemoryParticles } from "@/lib/vitals";
import type { JoyEvent, LiveTaskSnapshot } from "@/lib/types";
import { AmbientField } from "./AmbientField";
import { VillageCompanions } from "./VillageCompanions";
import { CelebrationLayer } from "./CelebrationLayer";
import { ConvergenceOrb } from "./ConvergenceOrb";
import { ObservatoryPanel } from "./ObservatoryPanel";
import { FrictionPanel } from "./FrictionPanel";
import { HarmonyFacets } from "./HarmonyFacets";
import { HarmonyPathways } from "./HarmonyPathways";
import { HabitatNav } from "./HabitatNav";
import { MemoryWhispers } from "./MemoryWhispers";
import { MomentumBars } from "./MomentumBars";
import { IntentGarden } from "./IntentGarden";

export function HabitatShell({
  snapshot,
  boardTasks,
  events,
  pulseTicks,
  biome,
  seed,
  connLabel,
  onNudge,
  onDepart,
  onSelectTask,
}: {
  snapshot: LiveTaskSnapshot;
  boardTasks: BoardTask[];
  events: JoyEvent[];
  pulseTicks: number;
  biome: VillageBiome;
  seed: IntentSeedKind;
  connLabel: string;
  onNudge: () => void;
  onDepart: () => void;
  onSelectTask?: (id: string) => void;
}) {
  const [tab, setTab] = useState<SanctuaryTab>("habitat");
  const [celebrate, setCelebrate] = useState(false);
  const [celebrateMsg, setCelebrateMsg] = useState("");
  const prevStabilizing = useRef(false);

  const vitals = useMemo(
    () => computeVitals(snapshot, boardTasks, pulseTicks, biome),
    [snapshot, boardTasks, pulseTicks, biome],
  );
  const stages = useMemo(() => buildPipelineStages(snapshot), [snapshot]);
  const companionMode = companionModeFromVitals(
    vitals.flow,
    vitals.confidence,
    vitals.stabilizing,
    vitals.resonanceFracture,
    vitals.isActive,
  );

  const whispers = useMemo(
    () => buildMemoryParticles(events, snapshot.display?.recentActivity ?? [], pulseTicks),
    [events, snapshot.display?.recentActivity, pulseTicks],
  );

  const growing =
    vitals.weather === "rain" || vitals.isActive || (snapshot.progress?.filesCopiedThisTick ?? 0) > 0;

  const reviewReady =
    vitals.stabilizing ||
    snapshot.display?.activityState === "review" ||
    snapshot.leaseStatus === "ReadyForReview";

  useEffect(() => {
    if (vitals.stabilizing && !prevStabilizing.current) {
      setCelebrateMsg("The garden is flourishing 🌸");
      setCelebrate(true);
      const t = setTimeout(() => setCelebrate(false), 2200);
      prevStabilizing.current = true;
      return () => clearTimeout(t);
    }
    if (!vitals.stabilizing) prevStabilizing.current = false;
  }, [vitals.stabilizing]);

  useEffect(() => {
    if (vitals.blooming && pulseTicks > 0 && pulseTicks % 50 === 0) {
      setCelebrateMsg("Harmony bloom ✨");
      setCelebrate(true);
      const t = setTimeout(() => setCelebrate(false), 1800);
      return () => clearTimeout(t);
    }
  }, [vitals.blooming, pulseTicks]);

  const seedMeta = INTENT_SEEDS.find((s) => s.id === seed);
  const biomeMeta = VILLAGE_BIOMES.find((b) => b.id === biome);

  return (
    <div className="relative min-h-[calc(100vh-7rem)] pb-28">
      <AmbientField weather={vitals.weather} />
      <CelebrationLayer show={celebrate} message={celebrateMsg} />

      <div className="relative z-10 space-y-4">
        <header className="flex flex-wrap items-start justify-between gap-2">
          <div>
            <p className="text-xs font-medium text-cozy-muted">
              {biomeMeta?.icon} {biomeMeta?.label} · {vitals.weatherEmoji}{" "}
              {vitals.weatherLabel}
            </p>
            <p className="text-sm text-cozy-cream/90">{vitals.weatherMood}</p>
          </div>
          <button
            type="button"
            onClick={onDepart}
            className="rounded-cozy cozy-card px-3 py-1.5 text-xs text-cozy-muted hover:text-cozy-cream"
          >
            Leave village
          </button>
        </header>

        {vitals.resonanceFracture && (
          <FrictionPanel
            snapshot={snapshot}
            whisper={vitals.fractureWhisper}
            onRefresh={onNudge}
            onObservatory={() => setTab("observatory")}
          />
        )}

        {tab === "habitat" && (
          <motion.div key="habitat" initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="space-y-4">
            <div className="cozy-panel p-5 sm:p-6">
              <ConvergenceOrb
                convergence={vitals.convergence}
                momentum={vitals.momentum}
                confidence={vitals.confidence}
                phaseWhisper={vitals.phaseWhisper}
                intentEcho={vitals.intentEcho}
                isActive={vitals.isActive}
                stabilizing={vitals.stabilizing}
                blooming={vitals.blooming}
              />
              <div className="mt-5 border-t border-cozy-elevated/60 pt-4">
                <VillageCompanions count={vitals.companionCount} mode={companionMode} />
              </div>
            </div>
            <div className="cozy-panel p-4">
              <p className="mb-2 text-xs font-semibold text-cozy-muted">Village whispers</p>
              <MemoryWhispers whispers={whispers} />
            </div>
            <button
              type="button"
              onClick={onNudge}
              className="w-full rounded-cozy-lg bg-cozy-sage/20 py-3 text-sm font-semibold text-cozy-sage"
            >
              Wave at the village 🌿
            </button>
          </motion.div>
        )}

        {tab === "garden" && (
          <motion.div key="garden" initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="space-y-4">
            <div className="cozy-panel p-5 text-center">
              <p className="text-3xl">{seedMeta?.icon}</p>
              <h2 className="mt-2 text-lg font-bold text-cozy-cream">{vitals.intentEcho}</h2>
              <p className="mt-1 text-sm text-cozy-muted">{seedMeta?.seedName} · {seedMeta?.promise}</p>
            </div>
            <IntentGarden
              tasks={boardTasks}
              activeTaskId={snapshot.taskId}
              growing={growing}
              onSelect={onSelectTask}
            />
          </motion.div>
        )}

        {tab === "flow" && (
          <motion.div key="flow" initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="space-y-4">
            <div className="cozy-panel p-4">
              <p className="mb-3 text-xs font-semibold text-cozy-muted">Village path</p>
              <HarmonyPathways stages={stages} />
            </div>
            <div className="cozy-panel p-4">
              <MomentumBars
                momentum={vitals.momentum}
                convergence={vitals.convergence}
                harmony={vitals.harmony}
                confidence={vitals.confidence}
                flow={vitals.flow}
                synthesisCharge={vitals.synthesisCharge}
              />
            </div>
          </motion.div>
        )}

        {tab === "harmony" && (
          <motion.div key="harmony" initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="space-y-4">
            <div className="cozy-panel p-4">
              <MomentumBars
                momentum={vitals.momentum}
                convergence={vitals.convergence}
                harmony={vitals.harmony}
                confidence={vitals.confidence}
                flow={vitals.flow}
                synthesisCharge={vitals.synthesisCharge}
              />
            </div>
            <div className="cozy-panel p-4">
              <p className="mb-3 text-xs font-semibold text-cozy-muted">Harmony facets</p>
              <HarmonyFacets items={snapshot.display?.deliverables ?? []} />
            </div>
          </motion.div>
        )}

        {tab === "review" && (
          <motion.div key="review" initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="space-y-4">
            <div className="cozy-panel p-6 text-center">
              <p className="text-5xl">{reviewReady ? "🌻" : "🌱"}</p>
              <h2 className="mt-3 text-lg font-bold text-cozy-cream">
                {reviewReady ? "Harvest time" : "Still growing"}
              </h2>
              <p className="mt-2 text-sm text-cozy-muted">
                {reviewReady
                  ? "The village made something lovely. Visit when you feel ready."
                  : "Companions are still tending your plot. Check back soon."}
              </p>
            </div>
            <HarmonyFacets items={snapshot.display?.deliverables ?? []} />
          </motion.div>
        )}

        {tab === "observatory" && (
          <motion.div key="observatory" initial={{ opacity: 0 }} animate={{ opacity: 1 }}>
            <ObservatoryPanel snapshot={snapshot} events={events} connLabel={connLabel} />
          </motion.div>
        )}
      </div>

      <HabitatNav active={tab} onChange={setTab} reviewPulse={reviewReady} />
    </div>
  );
}
