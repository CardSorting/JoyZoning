"use client";

import { useEffect, useState } from "react";
import { apiUrl } from "@/lib/config";
import { HabitatAuthorityChecklistPanel } from "@/components/HabitatAuthorityChecklistPanel";
import type { ActiveTaskSummary, WatchSession } from "@/lib/types";

type HermesHealthSnapshot = {
  state?: string;
  message?: string;
  apiUrl?: string;
  runtimeOwner?: string;
  habitatRole?: string;
};

export function OperatorEntryFlow({
  sessions,
  activeTasks,
  sessionId,
  taskId,
  taskOptions,
  onSessionChange,
  onTaskChange,
  onEnter,
}: {
  sessions: WatchSession[];
  activeTasks: ActiveTaskSummary[];
  sessionId: string;
  taskId: string;
  taskOptions: { id: string; title: string; status: number }[];
  onSessionChange: (id: string) => void;
  onTaskChange: (id: string) => void;
  onEnter: () => void;
}) {
  const [step, setStep] = useState<"pick" | "confirm">("pick");
  const [runtimeHealth, setRuntimeHealth] = useState<HermesHealthSnapshot | null>(null);
  const [loadingHealth, setLoadingHealth] = useState(true);

  useEffect(() => {
    let active = true;
    const checkHealth = async () => {
      try {
        const res = await fetch(apiUrl("/api/hermes/health"), { credentials: "include" });
        if (res.ok) {
          const data = (await res.json()) as HermesHealthSnapshot;
          if (active) {
            setRuntimeHealth(data);
            setLoadingHealth(false);
          }
        } else if (active) {
          setRuntimeHealth(null);
          setLoadingHealth(false);
        }
      } catch {
        if (active) {
          setRuntimeHealth(null);
          setLoadingHealth(false);
        }
      }
    };
    void checkHealth();
    const interval = setInterval(checkHealth, 3000);
    return () => {
      active = false;
      clearInterval(interval);
    };
  }, []);

  const hasRuntime = runtimeHealth !== null;
  const isWorkspaceSelected = !!sessionId;
  const isGatewayOnline = runtimeHealth?.state === "Healthy";
  const selectedWorkspacePath = sessions.find((s) => s.id === sessionId)?.workspaceRoot || "";

  return (
    <div className="mx-auto max-w-lg space-y-6 px-1 pb-8 text-zinc-100">
      <header className="text-center">
        <p className="text-[10px] font-semibold uppercase tracking-widest text-zinc-500">
          JoyZoning Watch
        </p>
        <h1 className="mt-2 text-2xl font-bold">Operator console</h1>
        <p className="mt-2 text-sm text-zinc-400">
          JoyZoning supervises execution. Hermes runs tools; you review and accept-merge here.
        </p>
      </header>

      <div className="rounded-2xl border border-zinc-800 bg-zinc-950/60 p-5 space-y-4">
        <h2 className="text-xs font-bold uppercase tracking-wider text-zinc-400">
          Habitat onboarding
        </h2>

        <div className="space-y-3 text-sm">
          <div className="flex items-center justify-between">
            <span className="text-zinc-300">Step 1: Connect Hermes runtime</span>
            <span
              className={`rounded border px-2 py-0.5 text-xs font-medium ${hasRuntime ? "border-emerald-500/20 bg-emerald-500/10 text-emerald-400" : "border-rose-500/20 bg-rose-500/10 text-rose-400"}`}
            >
              {loadingHealth ? "Checking…" : hasRuntime ? "Reachable" : "Not found"}
            </span>
          </div>

          <div className="flex items-center justify-between">
            <span className="text-zinc-300">Step 2: Select workspace</span>
            <span
              className={`rounded border px-2 py-0.5 text-xs font-medium ${isWorkspaceSelected ? "border-emerald-500/20 bg-emerald-500/10 text-emerald-400" : "border-zinc-700 bg-zinc-800 text-zinc-400"}`}
            >
              {isWorkspaceSelected ? "Selected" : "Choose below"}
            </span>
          </div>

          <div className="flex items-center justify-between">
            <span className="text-zinc-300">Step 3: Hermes gateway ready</span>
            <span
              className={`rounded border px-2 py-0.5 text-xs font-medium ${isGatewayOnline ? "border-emerald-500/20 bg-emerald-500/10 text-emerald-400" : "border-rose-500/20 bg-rose-500/10 text-rose-400"}`}
            >
              {isGatewayOnline ? "Healthy" : "Offline"}
            </span>
          </div>
        </div>

        {hasRuntime && (
          <div className="space-y-1 border-t border-zinc-900 pt-3 text-xs text-zinc-500">
            <div>
              <span className="text-zinc-400">Runtime owner:</span>{" "}
              {runtimeHealth?.runtimeOwner ?? "hermes"}
            </div>
            <div>
              <span className="text-zinc-400">Habitat role:</span>{" "}
              {runtimeHealth?.habitatRole ?? "observe-only"}
            </div>
            {runtimeHealth?.apiUrl && (
              <div>
                <span className="text-zinc-400">Hermes API:</span> {runtimeHealth.apiUrl}
              </div>
            )}
            {isWorkspaceSelected && (
              <div>
                <span className="text-zinc-400">Workspace:</span> {selectedWorkspacePath}
              </div>
            )}
            {runtimeHealth?.message && (
              <div className="text-zinc-400">{runtimeHealth.message}</div>
            )}
          </div>
        )}
      </div>

      <HabitatAuthorityChecklistPanel />

      {step === "pick" && (
        <div className="space-y-4 rounded-2xl border border-zinc-700 bg-zinc-900/60 p-5">
          <label className="block">
            <span className="text-xs font-medium text-zinc-500">Workspace session</span>
            <select
              className="mt-1 w-full rounded-lg border border-zinc-600 bg-zinc-950 px-3 py-2.5 text-sm"
              value={sessionId}
              onChange={(e) => {
                onSessionChange(e.target.value);
                onTaskChange("");
              }}
            >
              <option value="">Choose session…</option>
              {sessions.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name || s.workspaceRoot}
                </option>
              ))}
            </select>
          </label>

          <label className="block">
            <span className="text-xs font-medium text-zinc-500">Task</span>
            <select
              className="mt-1 w-full rounded-lg border border-zinc-600 bg-zinc-950 px-3 py-2.5 text-sm"
              value={taskId}
              onChange={(e) => onTaskChange(e.target.value)}
              disabled={!sessionId}
            >
              <option value="">Choose task…</option>
              {taskOptions.map((t) => (
                <option key={t.id} value={t.id}>
                  {(t.title || t.id).slice(0, 60)}
                </option>
              ))}
            </select>
          </label>

          {activeTasks.length > 0 && (
            <div className="flex flex-wrap gap-2">
              <span className="w-full text-[10px] uppercase tracking-widest text-zinc-500">
                Supervised runs
              </span>
              {activeTasks.slice(0, 6).map((t) => (
                <button
                  key={t.taskId}
                  type="button"
                  onClick={() => {
                    onSessionChange(t.sessionId);
                    onTaskChange(t.taskId);
                  }}
                  className="rounded-full border border-sky-500/40 bg-sky-500/10 px-3 py-1 text-xs text-sky-200"
                >
                  {t.title.slice(0, 28)}
                  {t.title.length > 28 ? "…" : ""} · {t.leaseStatus}
                </button>
              ))}
            </div>
          )}

          <button
            type="button"
            disabled={!taskId || !isGatewayOnline}
            onClick={() => setStep("confirm")}
            className="w-full rounded-xl bg-sky-600 py-3 text-sm font-bold text-white disabled:opacity-40"
          >
            {!isGatewayOnline ? "Waiting for Hermes runtime…" : "Continue"}
          </button>
        </div>
      )}

      {step === "confirm" && (
        <div className="space-y-4 rounded-2xl border border-zinc-700 bg-zinc-900/60 p-5 text-center">
          <p className="text-sm text-zinc-400">
            Open the unified console for this task. Review, accept-merge, and revoke stay on one
            page — JoyZoning does not execute tools directly.
          </p>
          <button
            type="button"
            onClick={onEnter}
            className="w-full rounded-xl bg-sky-600 py-3 text-sm font-bold text-white"
          >
            Open console
          </button>
          <button
            type="button"
            onClick={() => setStep("pick")}
            className="text-xs text-zinc-500 hover:text-zinc-300"
          >
            Back
          </button>
        </div>
      )}
    </div>
  );
}
