"use client";

/**
 * Execution Mode — JSDP worker orchestration (canonical workspace, sequential roles).
 * @see docs/operational-modes.md
 */

import { useCallback, useEffect, useState } from "react";
import { AlertTriangle, Copy, ExternalLink, Layers, Shield } from "lucide-react";
import { api } from "@/lib/api";
import { copyPath, openPathInShell } from "@/lib/path-actions";
import type { ParallelWorkersSnapshot, ParallelWorkerEntry } from "@/lib/parallel-workers";
import { ModeHandoffLink } from "./ModeHandoffLink";
import type { JoyZoningOperationalMode } from "@/lib/operational-modes";
import { isOperationalMode } from "@/lib/operational-modes";
import { PathActionNotice } from "./PathActionNotice";

function shortId(id: string) {
  return id.length > 12 ? `${id.slice(0, 8)}…` : id;
}

function WorkerCard({
  worker,
  isSelected,
  onSelect,
  inspectOnly,
  onGoToReview,
  onPathNotice,
  onNavigateMode,
  activeMode,
}: {
  worker: ParallelWorkerEntry;
  isSelected: boolean;
  onSelect?: () => void;
  inspectOnly?: boolean;
  onGoToReview?: () => void;
  onPathNotice: (message: string | null, variant?: "info" | "error") => void;
  onNavigateMode?: (mode: JoyZoningOperationalMode) => void;
  activeMode?: JoyZoningOperationalMode;
}) {
  const showReviewHandoff =
    inspectOnly &&
    onGoToReview &&
    (worker.recommendedMode === "review" ||
      worker.mergeState === "ready_to_merge" ||
      worker.mergeState === "merge_conflict");

  const shellClass = `rounded-xl border p-3 transition-colors ${
    isSelected
      ? "border-campfire-accent/60 bg-campfire-accent/10"
      : "border-campfire-border bg-campfire-elevated/50"
  }`;

  const summary = (
    <>
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="truncate font-semibold text-campfire-text">{worker.taskTitle}</p>
          <p className="mt-0.5 text-[11px] text-campfire-muted">
            Kanban: {worker.kanbanStatus} · rev {worker.kanbanPushedRevision}/
            {worker.kanbanRevision}
          </p>
        </div>
        <span className="shrink-0 rounded-full border border-campfire-border px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-campfire-muted">
          {worker.taskExecutionMode === "ExternalAgent"
            ? `External: ${worker.externalAgentName ?? worker.executionDriver ?? "agent"}`
            : worker.leaseStatus}
        </span>
      </div>

      <dl className="mt-3 grid gap-1.5 text-[11px] text-campfire-muted">
        {worker.taskExecutionMode === "ExternalAgent" ? (
          <>
            <div className="flex justify-between gap-2">
              <dt>Execution</dt>
              <dd className="text-campfire-text">
                External: {worker.externalAgentName ?? worker.executionDriver ?? "agent"}
              </dd>
            </div>
            {worker.branchName && (
              <div className="flex justify-between gap-2">
                <dt>Branch</dt>
                <dd className="font-mono text-campfire-text">{worker.branchName}</dd>
              </div>
            )}
            {worker.lastWorkspaceScanAt && (
              <div className="flex justify-between gap-2">
                <dt>Last scan</dt>
                <dd className="font-mono text-campfire-text">
                  {new Date(worker.lastWorkspaceScanAt).toLocaleString()}
                </dd>
              </div>
            )}
            {worker.changedFiles && worker.changedFiles.length > 0 && (
              <div>
                <dt>Changed files</dt>
                <dd className="mt-0.5 max-h-20 overflow-y-auto font-mono text-[10px] text-campfire-text">
                  {worker.changedFiles.slice(0, 8).join(", ")}
                  {worker.changedFiles.length > 8 ? ` (+${worker.changedFiles.length - 8})` : ""}
                </dd>
              </div>
            )}
          </>
        ) : (
          <>
            <div className="flex justify-between gap-2">
              <dt>Execution</dt>
              <dd className="font-mono text-campfire-text">
                {worker.executionSessionId ? shortId(worker.executionSessionId) : "Managed by Hermes"}
              </dd>
            </div>
            <div className="flex justify-between gap-2">
              <dt>Hermes session</dt>
              <dd className="truncate font-mono text-campfire-text">
                {worker.hermesSessionId ? shortId(worker.hermesSessionId) : "—"}
              </dd>
            </div>
            <div className="flex justify-between gap-2">
              <dt>Lease</dt>
              <dd className="font-mono">{shortId(worker.leaseId)}</dd>
            </div>
          </>
        )}
      </dl>

      {worker.workspacePath && (
        <p className="mt-2 break-all font-mono text-[10px] text-campfire-muted/90">
          {worker.workspacePath}
        </p>
      )}
    </>
  );

  return (
    <div className={shellClass} data-selected={isSelected || undefined}>
      {onSelect ? (
        <button type="button" onClick={onSelect} className="w-full text-left">
          {summary}
        </button>
      ) : (
        <div>{summary}</div>
      )}

      {worker.workspacePath && (
        <div className="mt-2 flex flex-wrap gap-2">
          <button
            type="button"
            onClick={() => {
              void copyPath(worker.workspacePath!).then((res) =>
                onPathNotice(res.ok ? "Workspace path copied" : res.error, res.ok ? "info" : "error"),
              );
            }}
            className="inline-flex items-center gap-1 rounded-lg border border-campfire-border px-2 py-1 text-[10px] text-campfire-muted hover:text-campfire-text"
          >
            <Copy className="h-3 w-3" />
            Copy path
          </button>
          <button
            type="button"
            onClick={() => {
              void openPathInShell(worker.workspacePath!).then((res) =>
                onPathNotice(res.ok ? "Opened workspace" : res.error, res.ok ? "info" : "error"),
              );
            }}
            className="inline-flex items-center gap-1 rounded-lg border border-campfire-border px-2 py-1 text-[10px] text-campfire-muted hover:text-campfire-text"
          >
            <ExternalLink className="h-3 w-3" />
            Open workspace
          </button>
        </div>
      )}

      {showReviewHandoff && onGoToReview && (
        <button
          type="button"
          onClick={() => onGoToReview()}
          className="mt-2 text-[10px] font-semibold text-campfire-accent hover:underline"
        >
          Review merge readiness below
        </button>
      )}

      {isSelected &&
        onNavigateMode &&
        worker.availableModeTransitions &&
        worker.availableModeTransitions.length > 0 && (
          <div className="mt-2 space-y-1 border-t border-campfire-border/50 pt-2">
            {worker.availableModeTransitions
              .filter(
                (t) =>
                  isOperationalMode(t.targetMode) &&
                  t.targetMode !== activeMode,
              )
              .slice(0, 2)
              .map((t) => (
                <ModeHandoffLink
                  key={`${t.targetMode}-${t.handoffKind ?? t.label}`}
                  targetMode={t.targetMode as JoyZoningOperationalMode}
                  label={t.label}
                  reason={t.reason}
                  onNavigate={onNavigateMode}
                  theme="campfire"
                />
              ))}
          </div>
        )}
    </div>
  );
}

export function ParallelWorkersPanel({
  sessionId,
  selectedTaskId,
  onSelectTask,
  theme = "campfire",
  inspectOnly = false,
  onGoToReview,
  onNavigateMode,
  activeMode = "execution",
}: {
  sessionId: string | null;
  selectedTaskId?: string | null;
  onSelectTask?: (taskId: string) => void;
  theme?: "campfire" | "pet";
  /** Execution mode: no merge actions; offer handoff to Review. */
  inspectOnly?: boolean;
  onGoToReview?: () => void;
  onNavigateMode?: (mode: JoyZoningOperationalMode) => void;
  activeMode?: JoyZoningOperationalMode;
}) {
  const [data, setData] = useState<ParallelWorkersSnapshot | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [pathNotice, setPathNotice] = useState<{
    message: string;
    variant: "info" | "error";
  } | null>(null);

  const reportPathNotice = useCallback(
    (message: string | null, variant: "info" | "error" = "info") => {
      if (!message) {
        setPathNotice(null);
        return;
      }
      setPathNotice({ message, variant });
      window.setTimeout(() => setPathNotice(null), 3500);
    },
    [],
  );

  const load = useCallback(async () => {
    if (!sessionId) {
      setData(null);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const snap = await api.parallelWorkers(sessionId);
      setData(snap);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load parallel workers");
      setData(null);
    } finally {
      setLoading(false);
    }
  }, [sessionId]);

  useEffect(() => {
    void load();
    const t = setInterval(() => void load(), 8000);
    return () => clearInterval(t);
  }, [load]);

  const border = theme === "pet" ? "border-pet-elevated" : "border-campfire-border";
  const surface = theme === "pet" ? "bg-pet-deep/80" : "bg-campfire-surface/80";
  const muted = theme === "pet" ? "text-pet-muted" : "text-campfire-muted";
  const text = theme === "pet" ? "text-pet-cream" : "text-campfire-text";

  if (!sessionId) return null;

  const protocol = data?.protocol ?? "jsdp";

  return (
    <section
      data-joyzoning-mode="execution"
      data-canonical-surface="true"
      className={`rounded-2xl border ${border} ${surface} p-4`}
    >
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <Layers className={`h-4 w-4 ${muted}`} />
          <h3 className={`text-sm font-bold uppercase tracking-wide ${text}`}>
            Parallel workers
          </h3>
        </div>
        <span className="rounded-full border border-emerald-500/40 bg-emerald-500/10 px-2 py-0.5 text-[10px] font-semibold uppercase text-emerald-300">
          {protocol}
        </span>
      </div>

      {inspectOnly && (
        <p className={`mb-3 text-xs ${muted}`}>
          Inspect runtime only — merge and revoke live in Review mode.
        </p>
      )}

      {error && (
        <p className="mb-2 text-xs text-red-400" role="alert">
          {error}
        </p>
      )}

      <PathActionNotice
        message={pathNotice?.message ?? null}
        variant={pathNotice?.variant ?? "info"}
      />

      {loading && !data && <p className={`text-xs ${muted}`}>Loading workers…</p>}

      {data && data.warnings.length > 0 && (
        <ul className="mb-3 space-y-1">
          {data.warnings.map((w, i) => (
            <li
              key={`${w.code}-${i}`}
              className="flex gap-2 rounded-lg border border-amber-500/30 bg-amber-500/5 px-2 py-1.5 text-[11px] text-amber-100"
            >
              <AlertTriangle className="h-3.5 w-3.5 shrink-0" />
              {w.message}
            </li>
          ))}
        </ul>
      )}

      {data && data.workers.length === 0 && (
        <p className={`text-xs ${muted}`}>No active workers on this workspace.</p>
      )}

      <div className="grid gap-2 sm:grid-cols-2">
        {data?.workers.map((w) => (
          <WorkerCard
            key={`${w.leaseId}-${w.workspacePath ?? w.taskId}`}
            worker={w}
            isSelected={selectedTaskId === w.taskId}
            onSelect={onSelectTask ? () => onSelectTask(w.taskId) : undefined}
            inspectOnly={inspectOnly}
            onGoToReview={onGoToReview}
            onPathNotice={reportPathNotice}
            onNavigateMode={onNavigateMode}
            activeMode={activeMode}
          />
        ))}
      </div>
    </section>
  );
}
