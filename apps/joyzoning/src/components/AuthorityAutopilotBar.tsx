"use client";

import { useCallback, useState } from "react";
import { Bot, RefreshCw, Shield } from "lucide-react";
import { api } from "@/lib/api";
import type { SessionAuthority } from "@/lib/authority";

export function AuthorityAutopilotBar({
  authority,
  autoAcceptedCount,
  needsReviewCount,
  sessionId,
  onReconciled,
}: {
  authority: SessionAuthority;
  autoAcceptedCount: number;
  needsReviewCount: number;
  sessionId?: string;
  onReconciled?: () => void;
}) {
  const [reconciling, setReconciling] = useState(false);
  const [reconcileNotice, setReconcileNotice] = useState<string | null>(null);

  const runReconcile = useCallback(async () => {
    if (!sessionId || !authority.autopilotEnabled) return;
    setReconciling(true);
    setReconcileNotice(null);
    try {
      const report = await api.reconcileAuthority(sessionId);
      setReconcileNotice(
        `Reconciled ${report.evaluated} worker(s): ${report.autoAccepted} auto-accepted, ${report.blocked} blocked.`,
      );
      onReconciled?.();
    } catch (e) {
      setReconcileNotice(e instanceof Error ? e.message : "Reconcile failed");
    } finally {
      setReconciling(false);
      window.setTimeout(() => setReconcileNotice(null), 4000);
    }
  }, [sessionId, authority.autopilotEnabled, onReconciled]);
  return (
    <section
      data-testid="authority-autopilot-bar"
      className="rounded-xl border border-amber-500/35 bg-amber-950/25 px-4 py-3"
    >
      <div className="flex flex-wrap items-center gap-2">
        <Bot className="h-4 w-4 text-amber-300" />
        <h2 className="text-xs font-bold uppercase tracking-widest text-amber-200">
          Autopilot · bounded YOLO
        </h2>
        <span className="rounded-full border border-amber-500/40 bg-amber-500/10 px-2 py-0.5 text-[10px] font-semibold text-amber-100">
          {authority.profileLabel}
        </span>
        {!authority.autopilotEnabled && (
          <span className="text-[10px] text-amber-400/80">(autopilot off)</span>
        )}
      </div>
      <p className="mt-1 text-[11px] text-zinc-400">
        Policy decides safe auto-accept into the main workspace. You only review what policy blocks.
      </p>
      <div className="mt-2 flex flex-wrap items-center gap-3 text-[10px]">
        <span className="inline-flex items-center gap-1 text-emerald-300">
          <Shield className="h-3 w-3" />
          {autoAcceptedCount} auto-accepted
        </span>
        <span className="text-zinc-400">{needsReviewCount} needs review</span>
        {sessionId && authority.autopilotEnabled && (
          <button
            type="button"
            data-testid="authority-reconcile"
            disabled={reconciling}
            onClick={() => void runReconcile()}
            className="inline-flex items-center gap-1 rounded border border-amber-500/40 px-2 py-0.5 text-amber-200 hover:bg-amber-500/10 disabled:opacity-50"
          >
            <RefreshCw className={`h-3 w-3 ${reconciling ? "animate-spin" : ""}`} />
            Reconcile pending
          </button>
        )}
      </div>
      {reconcileNotice && (
        <p className="mt-2 text-[10px] text-amber-100/90" role="status">
          {reconcileNotice}
        </p>
      )}
    </section>
  );
}

export function AutopilotActivityFeed({
  accepted,
  blocked,
}: {
  accepted: { taskTitle: string; taskId: string; at?: string | null }[];
  blocked: { taskTitle: string; taskId: string; message: string }[];
}) {
  if (accepted.length === 0 && blocked.length === 0) return null;

  return (
    <section
      data-testid="autopilot-activity"
      className="rounded-xl border border-zinc-700 bg-zinc-900/50 p-3"
    >
      <h3 className="mb-2 text-[10px] font-bold uppercase tracking-widest text-zinc-500">
        Autopilot activity
      </h3>
      {accepted.length > 0 && (
        <ul className="mb-2 space-y-1 text-[11px] text-emerald-300/90">
          {accepted.map((a) => (
            <li key={a.taskId}>
              Auto-accepted: <span className="font-semibold">{a.taskTitle}</span>
            </li>
          ))}
        </ul>
      )}
      {blocked.length > 0 && (
        <ul className="space-y-1 text-[11px] text-amber-200/90">
          {blocked.map((b) => (
            <li key={b.taskId}>
              <span className="font-semibold">{b.taskTitle}</span> — {b.message}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
