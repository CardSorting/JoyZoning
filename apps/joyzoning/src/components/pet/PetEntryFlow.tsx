"use client";

import { useState } from "react";
import { motion, AnimatePresence } from "framer-motion";
import { INTENT_OPTIONS, type IntentKind } from "@/lib/pet";
import { saveIntent } from "@/lib/session-prefs";
import type { ActiveTaskSummary, WatchSession } from "@/lib/types";

type Step = "welcome" | "intent" | "task" | "ready";

export function PetEntryFlow({
  sessions,
  activeTasks,
  sessionId,
  taskId,
  taskOptions,
  initialIntent,
  onSessionChange,
  onTaskChange,
  onEnter,
}: {
  sessions: WatchSession[];
  activeTasks: ActiveTaskSummary[];
  sessionId: string;
  taskId: string;
  taskOptions: { id: string; title: string; status: number }[];
  initialIntent: IntentKind;
  onSessionChange: (id: string) => void;
  onTaskChange: (id: string) => void;
  onEnter: (intent: IntentKind) => void;
}) {
  const [step, setStep] = useState<Step>("welcome");
  const [intent, setIntent] = useState<IntentKind>(initialIntent);

  const hatch = () => {
    saveIntent(intent);
    onEnter(intent);
  };

  return (
    <div className="mx-auto max-w-lg px-1 pb-8">
      <AnimatePresence mode="wait">
        {step === "welcome" && (
          <motion.div
            key="welcome"
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0 }}
            className="text-center"
          >
            <p className="text-5xl" aria-hidden>
              ◈
            </p>
            <h1 className="mt-4 text-3xl font-bold text-pet-cream">JoyZone Watch</h1>
            <p className="mx-auto mt-3 max-w-sm text-sm leading-relaxed text-pet-muted">
              Tamagotchi for supervised runtime health — one synthesis pet, real stack traces in
              the basement.
            </p>
            <ul className="mx-auto mt-6 max-w-xs space-y-2 text-left text-xs text-pet-muted">
              <li>• Is the system okay?</li>
              <li>• What is it doing?</li>
              <li>• Does it need me?</li>
            </ul>
            <button
              type="button"
              onClick={() => setStep("intent")}
              className="mt-10 w-full rounded-pet-lg bg-pet-mint/90 py-4 text-sm font-bold text-pet-night shadow-pet"
            >
              Hatch your pet
            </button>
          </motion.div>
        )}

        {step === "intent" && (
          <motion.div key="intent" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}>
            <h2 className="text-center text-xl font-bold text-pet-cream">Feed intent type</h2>
            <p className="mt-2 text-center text-sm text-pet-muted">
              How should your pet interpret this run? (Cosmetic label — real work comes from the task.)
            </p>
            <ul className="mt-6 space-y-2">
              {INTENT_OPTIONS.map((opt) => (
                <li key={opt.id}>
                  <button
                    type="button"
                    onClick={() => setIntent(opt.id)}
                    className={`pet-card flex w-full items-center gap-3 p-4 text-left ${
                      intent === opt.id ? "border-pet-mint/50 bg-pet-mint/10" : ""
                    }`}
                  >
                    <span className="text-2xl">{opt.icon}</span>
                    <div>
                      <p className="font-semibold text-pet-cream">{opt.label}</p>
                      <p className="text-xs text-pet-muted">{opt.blurb}</p>
                    </div>
                  </button>
                </li>
              ))}
            </ul>
            <Nav onBack={() => setStep("welcome")} onNext={() => setStep("task")} />
          </motion.div>
        )}

        {step === "task" && (
          <motion.div key="task" initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="pet-panel space-y-4 p-5">
            <h2 className="text-lg font-bold text-pet-cream">Attach to a run</h2>
            <label className="block">
              <span className="text-xs font-medium text-pet-muted">Session</span>
              <select
                className="mt-1 w-full rounded-pet border border-pet-elevated bg-pet-deep px-4 py-3 text-pet-cream"
                value={sessionId}
                onChange={(e) => {
                  onSessionChange(e.target.value);
                  onTaskChange("");
                }}
              >
                <option value="">Choose…</option>
                {sessions.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name || s.workspaceRoot || s.id}
                  </option>
                ))}
              </select>
            </label>
            <label className="block">
              <span className="text-xs font-medium text-pet-muted">Task</span>
              <select
                className="mt-1 w-full rounded-pet border border-pet-elevated bg-pet-deep px-4 py-3 text-pet-cream"
                value={taskId}
                onChange={(e) => onTaskChange(e.target.value)}
                disabled={!sessionId}
              >
                <option value="">Choose…</option>
                {taskOptions.map((t) => (
                  <option key={t.id} value={t.id}>
                    {(t.title || t.id).slice(0, 50)}
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
                    className="rounded-full bg-pet-mint/15 px-3 py-1 text-xs text-pet-mint"
                  >
                    live run
                  </button>
                ))}
              </div>
            )}
            <Nav
              onBack={() => setStep("intent")}
              onNext={() => setStep("ready")}
              nextDisabled={!taskId}
              nextLabel="Continue"
            />
          </motion.div>
        )}

        {step === "ready" && (
          <motion.div key="ready" className="pet-panel p-6 text-center">
            <p className="text-4xl">◈</p>
            <h2 className="mt-4 text-xl font-bold text-pet-cream">Ready to watch</h2>
            <p className="mt-3 text-sm text-pet-muted">
              Your synthesis pet will mirror supervised runtime health. When things break, the
              stack trace lives in Observatory — not hidden, not scary-first.
            </p>
            <button
              type="button"
              disabled={!taskId}
              onClick={hatch}
              className="mt-8 w-full rounded-pet-lg bg-pet-mint/90 py-4 text-sm font-bold text-pet-night disabled:opacity-40"
            >
              Start watching
            </button>
            <button
              type="button"
              onClick={() => setStep("task")}
              className="mt-2 w-full py-2 text-xs text-pet-muted"
            >
              Change task
            </button>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

function Nav({
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
      <button type="button" onClick={onBack} className="flex-1 py-3 text-sm text-pet-muted">
        Back
      </button>
      <button
        type="button"
        disabled={nextDisabled}
        onClick={onNext}
        className="flex-[2] rounded-pet bg-pet-elevated py-3 text-sm font-semibold text-pet-mint disabled:opacity-40"
      >
        {nextLabel}
      </button>
    </div>
  );
}
