"use client";

import { useState, useEffect } from "react";
import type { ActiveTaskSummary, WatchSession } from "@/lib/types";

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
  const [runtimeHealth, setRuntimeHealth] = useState<any>(null);
  const [loadingHealth, setLoadingHealth] = useState(true);

  // Poll runtime health
  useEffect(() => {
    let active = true;
    const checkHealth = async () => {
      try {
        const res = await fetch("http://127.0.0.1:9090/health");
        if (res.ok) {
          const data = await res.json();
          if (active) {
            setRuntimeHealth(data);
            setLoadingHealth(false);
          }
        } else {
          if (active) {
            setRuntimeHealth(null);
            setLoadingHealth(false);
          }
        }
      } catch (err) {
        if (active) {
          setRuntimeHealth(null);
          setLoadingHealth(false);
        }
      }
    };
    checkHealth();
    const interval = setInterval(checkHealth, 3000);
    return () => {
      active = false;
      clearInterval(interval);
    };
  }, []);

  const hasRuntime = runtimeHealth !== null;
  const isWorkspaceSelected = !!sessionId;
  const isGatewayOnline = runtimeHealth?.status === "healthy";
  const isContainmentStrict = runtimeHealth?.containmentStatus?.strictMode === true;
  const selectedWorkspacePath = sessions.find((s) => s.id === sessionId)?.workspaceRoot || "";

  return (
    <div className="mx-auto max-w-lg space-y-6 px-1 pb-8 text-zinc-100">
      <header className="text-center">
        <p className="text-[10px] font-semibold uppercase tracking-widest text-zinc-500">
          JoyZoning Watch
        </p>
        <h1 className="mt-2 text-2xl font-bold">Operator console</h1>
        <p className="mt-2 text-sm text-zinc-400">
          One screen — task status, workers, merge actions, and live events. No mode hunting.
        </p>
      </header>

      {/* Onboarding Visual Checklist */}
      <div className="rounded-2xl border border-zinc-800 bg-zinc-950/60 p-5 space-y-4">
        <h2 className="text-xs font-bold uppercase tracking-wider text-zinc-400">
          Onboarding & Containment Status
        </h2>
        
        <div className="space-y-3 text-sm">
          {/* Step 1 */}
          <div className="flex items-center justify-between">
            <span className="text-zinc-300">Step 1: Detect Runtime</span>
            <span className={`px-2 py-0.5 rounded text-xs font-medium ${hasRuntime ? "bg-emerald-500/10 text-emerald-400 border border-emerald-500/20" : "bg-rose-500/10 text-rose-400 border border-rose-500/20"}`}>
              {hasRuntime ? "Detected" : "Not Found"}
            </span>
          </div>

          {/* Step 2 */}
          <div className="flex items-center justify-between">
            <span className="text-zinc-300">Step 2: Validate Workspace</span>
            <span className={`px-2 py-0.5 rounded text-xs font-medium ${isWorkspaceSelected ? "bg-emerald-500/10 text-emerald-400 border border-emerald-500/20" : "bg-zinc-800 text-zinc-400"}`}>
              {isWorkspaceSelected ? "Valid" : "Select Below"}
            </span>
          </div>

          {/* Step 3 */}
          <div className="flex items-center justify-between">
            <span className="text-zinc-300">Step 3: Start Local Agent Runtime</span>
            <span className={`px-2 py-0.5 rounded text-xs font-medium ${isGatewayOnline ? "bg-emerald-500/10 text-emerald-400 border border-emerald-500/20" : "bg-rose-500/10 text-rose-400 border border-rose-500/20"}`}>
              {isGatewayOnline ? "Gateway Online" : "Gateway Offline"}
            </span>
          </div>

          {/* Step 4 */}
          <div className="flex items-center justify-between">
            <span className="text-zinc-300">Step 4: Confirm Containment</span>
            <span className={`px-2 py-0.5 rounded text-xs font-medium ${isContainmentStrict ? "bg-emerald-500/10 text-emerald-400 border border-emerald-500/20" : "bg-amber-500/10 text-amber-400 border border-amber-500/20"}`}>
              {isContainmentStrict ? "Strict Containment" : "Soft Sandbox"}
            </span>
          </div>
        </div>

        {/* Diagnostic Metadata */}
        {hasRuntime && (
          <div className="pt-3 border-t border-zinc-900 space-y-1 text-xs text-zinc-500">
            <div><span className="text-zinc-400">Workspace Root:</span> {runtimeHealth?.allowedWorkspaceRoot}</div>
            {isWorkspaceSelected && (
              <div><span className="text-zinc-400">Active Workspace:</span> {selectedWorkspacePath}</div>
            )}
            <div><span className="text-zinc-400">Blocked Attempts:</span> {runtimeHealth?.containmentStatus?.blockedWritesCount || 0}</div>
            {runtimeHealth?.containmentStatus?.lastBlockedAttempt && (
              <div className="text-rose-400">
                <span className="text-zinc-400">Last Blocked:</span> {runtimeHealth.containmentStatus.lastBlockedAttempt.path}
              </div>
            )}
          </div>
        )}
      </div>

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
                Active runs
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
            {!isGatewayOnline ? "Waiting for Agent Runtime..." : "Continue"}
          </button>
        </div>
      )}

      {step === "confirm" && (
        <div className="space-y-4 rounded-2xl border border-zinc-700 bg-zinc-900/60 p-5 text-center">
          <p className="text-sm text-zinc-400">
            Open the unified console for this task. Approve, revoke, and workspace actions stay on
            one page.
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
