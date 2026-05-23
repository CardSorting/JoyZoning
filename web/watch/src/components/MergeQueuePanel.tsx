"use client";

/**
 * Review Mode — GitHub PR mental model (merge queue, verification, approve/revoke).
 * Do not fold kanban backlog or habitat pet state into this panel.
 * @see docs/operational-modes.md
 */

import { useCallback, useEffect, useState } from "react";
import { Copy, ExternalLink, GitMerge, RefreshCw, ShieldAlert, ShieldCheck, XCircle } from "lucide-react";
import { api } from "@/lib/api";
import {
  mergeStateLabel,
  mergeStateTone,
  shortCommit,
  type MergeQueueSnapshot,
  type MergeWorkerEntry,
} from "@/lib/merge-queue";
import type { OperatorDecisionAction } from "@/lib/operator-decision";
import { copyPath, openPathInShell } from "@/lib/path-actions";
import { PathActionNotice } from "./PathActionNotice";
import { WorkerDecisionConfirmDialog } from "./WorkerDecisionConfirmDialog";

function MergeWorkerCard({
  worker,
  isSelected,
  onSelect,
  onApprove,
  onRevoke,
  onInspect,
  onPathNotice,
}: {
  worker: MergeWorkerEntry;
  isSelected: boolean;
  onSelect?: () => void;
  onApprove?: () => void;
  onRevoke?: () => void;
  onInspect?: () => void;
  onPathNotice: (message: string | null, variant?: "info" | "error") => void;
}) {
  const r = worker.mergeReadiness;
  const c = worker.mergeConflict;

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
            Lease {worker.leaseStatus} · {r?.changedFilesCount ?? 0} changed file
            {(r?.changedFilesCount ?? 0) === 1 ? "" : "s"}
          </p>
        </div>
        <span
          className={`shrink-0 rounded-full border px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide ${mergeStateTone(worker.mergeState)}`}
        >
          {mergeStateLabel(worker.mergeState)}
        </span>
      </div>

      {(r?.baseCommit || r?.headCommit) && (
        <p className="mt-2 font-mono text-[10px] text-campfire-muted">
          base {shortCommit(r.baseCommit)} → head {shortCommit(r.headCommit)}
        </p>
      )}

      {r && r.changedFilesSummary.length > 0 && (
        <p className="mt-1 line-clamp-2 text-[11px] text-campfire-muted">
          {r.changedFilesSummary.join(", ")}
        </p>
      )}

      {r?.verificationSummary && (
        <p className="mt-1 text-[11px] text-campfire-muted">{r.verificationSummary}</p>
      )}

      {c && (
        <p className="mt-2 text-[11px] text-red-300/90">
          {c.category}: {c.reason}
          {c.conflictFiles.length > 0 && ` (${c.conflictFiles.slice(0, 3).join(", ")})`}
        </p>
      )}

      {worker.decisionSummary?.riskFlags.active && worker.decisionSummary.riskFlags.active.length > 0 && (
        <p className="mt-2 text-[10px] text-amber-300/90">
          Risks: {worker.decisionSummary.riskFlags.active.join(" · ")}
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

      <div className="mt-3 flex flex-wrap gap-2">
        {onApprove && worker.mergeState === "ready_to_merge" && (
          <button
            type="button"
            className="inline-flex items-center gap-1 rounded-lg border border-emerald-500/50 bg-emerald-500/10 px-2 py-1 text-[10px] font-semibold text-emerald-300"
            onClick={(e) => {
              e.stopPropagation();
              onApprove();
            }}
          >
            <ShieldCheck className="h-3 w-3" /> Accept
          </button>
        )}
        {onInspect &&
          (worker.mergeState === "merge_conflict" || worker.mergeState === "merge_failed") && (
            <button
              type="button"
              className="inline-flex items-center gap-1 rounded-lg border border-red-500/50 bg-red-500/10 px-2 py-1 text-[10px] font-semibold text-red-300"
              onClick={(e) => {
                e.stopPropagation();
                onInspect();
              }}
            >
              <ShieldAlert className="h-3 w-3" /> Review conflict
            </button>
          )}
        {onRevoke &&
          worker.mergeState !== "merged" &&
          worker.mergeState !== "revoked" &&
          worker.mergeState !== "abandoned" && (
            <button
              type="button"
              className="inline-flex items-center gap-1 rounded-lg border border-orange-500/50 bg-orange-500/10 px-2 py-1 text-[10px] font-semibold text-orange-300"
              onClick={(e) => {
                e.stopPropagation();
                onRevoke();
              }}
            >
              <XCircle className="h-3 w-3" /> Revoke
            </button>
          )}
        {r?.worktreePath && (
          <>
            <button
              type="button"
              className="inline-flex items-center gap-1 rounded-lg border border-campfire-border px-2 py-1 text-[10px] font-semibold text-campfire-muted hover:text-campfire-text"
            onClick={() => {
              void copyPath(r.worktreePath!).then((res) =>
                onPathNotice(res.ok ? "Worktree path copied" : res.error, res.ok ? "info" : "error"),
              );
            }}
            >
              <Copy className="h-3 w-3" /> Worktree
            </button>
            <button
              type="button"
              className="inline-flex items-center gap-1 rounded-lg border border-campfire-border px-2 py-1 text-[10px] font-semibold text-campfire-muted hover:text-campfire-text"
            onClick={() => {
              void openPathInShell(r.worktreePath!).then((res) =>
                onPathNotice(res.ok ? "Opened worktree in shell" : res.error, res.ok ? "info" : "error"),
              );
            }}
            >
              <ExternalLink className="h-3 w-3" /> Open workspace
            </button>
          </>
        )}
        {(worker.liveMirrorPath || r?.liveMirrorPath) && (
          <button
            type="button"
            className="inline-flex items-center gap-1 rounded-lg border border-campfire-border px-2 py-1 text-[10px] font-semibold text-campfire-muted hover:text-campfire-text"
            onClick={() => {
              const p = worker.liveMirrorPath || r!.liveMirrorPath!;
              void openPathInShell(p).then((res) =>
                onPathNotice(res.ok ? "Opened mirror in shell" : res.error, res.ok ? "info" : "error"),
              );
            }}
          >
            <ExternalLink className="h-3 w-3" /> Open workspace
          </button>
        )}
      </div>
    </div>
  );
}

function Section({
  title,
  workers,
  emptyHint,
  selectedTaskId,
  onSelectTask,
  onOpenDecision,
  authoritativeActions,
  onPathNotice,
}: {
  title: string;
  workers: MergeWorkerEntry[];
  emptyHint: string;
  selectedTaskId?: string;
  onSelectTask?: (taskId: string) => void;
  onOpenDecision: (worker: MergeWorkerEntry, action: OperatorDecisionAction) => void;
  authoritativeActions: boolean;
  onPathNotice: (message: string | null, variant?: "info" | "error") => void;
}) {
  if (workers.length === 0) return null;

  return (
    <div className="space-y-2">
      <h4 className="text-[11px] font-bold uppercase tracking-widest text-campfire-muted">{title}</h4>
      <div className="space-y-2">
        {workers.map((w) => (
          <MergeWorkerCard
            key={w.leaseId}
            worker={w}
            isSelected={selectedTaskId === w.taskId}
            onSelect={onSelectTask ? () => onSelectTask(w.taskId) : undefined}
            onApprove={
              authoritativeActions && w.mergeState === "ready_to_merge"
                ? () => onOpenDecision(w, "approve")
                : undefined
            }
            onInspect={
              authoritativeActions &&
              (w.mergeState === "merge_conflict" || w.mergeState === "merge_failed")
                ? () => onOpenDecision(w, "inspect")
                : undefined
            }
            onRevoke={
              authoritativeActions &&
              w.mergeState !== "merged" &&
              w.mergeState !== "revoked" &&
              w.mergeState !== "abandoned"
                ? () => onOpenDecision(w, "revoke")
                : undefined
            }
            onPathNotice={onPathNotice}
          />
        ))}
      </div>
      {workers.length === 0 && (
        <p className="text-xs text-campfire-muted">{emptyHint}</p>
      )}
    </div>
  );
}

export function MergeQueuePanel({
  sessionId,
  selectedTaskId,
  onSelectTask,
  authoritativeActions = false,
  embedded = false,
  externalSnapshot,
  onExternalRefresh,
}: {
  sessionId: string;
  selectedTaskId?: string;
  onSelectTask?: (taskId: string) => void;
  /** When false, hides approve/revoke (legacy mode-tab layout only). */
  authoritativeActions?: boolean;
  /** Single-screen console: shared data, no duplicate mode surface marker. */
  embedded?: boolean;
  externalSnapshot?: MergeQueueSnapshot | null;
  onExternalRefresh?: () => void;
}) {
  const [data, setData] = useState<MergeQueueSnapshot | null>(externalSnapshot ?? null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [dialog, setDialog] = useState<{
    worker: MergeWorkerEntry;
    action: OperatorDecisionAction;
  } | null>(null);
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
    if (embedded && onExternalRefresh) {
      onExternalRefresh();
      return;
    }
    try {
      setError(null);
      const snap = await api.mergeQueue(sessionId);
      setData(snap);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load merge queue");
    } finally {
      setLoading(false);
    }
  }, [sessionId, embedded, onExternalRefresh]);

  useEffect(() => {
    if (embedded) {
      setData(externalSnapshot ?? null);
      setLoading(false);
      return;
    }
    void load();
    const t = setInterval(() => void load(), 12_000);
    return () => clearInterval(t);
  }, [load, embedded, externalSnapshot]);

  const hasAny =
    data &&
    (data.readyToMerge.length > 0 ||
      data.mergeConflicts.length > 0 ||
      data.completedWorkers.length > 0 ||
      data.revokedAbandoned.length > 0);

  return (
    <section
      {...(!embedded ? { "data-joyzoning-mode": "review", "data-canonical-surface": "true" } : {})}
      data-testid="merge-queue-panel"
      className={
        embedded
          ? "rounded-2xl border border-zinc-700 bg-zinc-900/50 p-4"
          : "rounded-2xl border border-campfire-border bg-campfire-surface/40 p-4"
      }
    >
      <div className="mb-3 flex items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <GitMerge className="h-4 w-4 text-campfire-accent" />
          <h3 className="text-sm font-bold text-campfire-text">Merge & reconciliation</h3>
        </div>
        <button
          type="button"
          onClick={() => void load()}
          className="rounded-lg p-1.5 text-campfire-muted hover:bg-campfire-elevated hover:text-campfire-text"
          aria-label="Refresh merge queue"
        >
          <RefreshCw className={`h-4 w-4 ${loading ? "animate-spin" : ""}`} />
        </button>
      </div>

      {error && <p className="text-xs text-red-300">{error}</p>}

      <PathActionNotice
        message={pathNotice?.message ?? null}
        variant={pathNotice?.variant ?? "info"}
      />

      {!authoritativeActions && !embedded && (
        <p className="mb-3 text-xs text-amber-200/90">
          Read-only — approve, revoke, and conflict review are on the operator console.
        </p>
      )}

      {!error && !hasAny && !loading && (
        <p className="text-xs text-campfire-muted">No workers awaiting merge or reconciliation.</p>
      )}

      {data && (
        <div className="space-y-4">
          <Section
            title="Ready to merge"
            workers={data.readyToMerge}
            emptyHint=""
            selectedTaskId={selectedTaskId}
            onSelectTask={onSelectTask}
            onOpenDecision={(w, a) => setDialog({ worker: w, action: a })}
            authoritativeActions={authoritativeActions}
            onPathNotice={reportPathNotice}
          />
          <Section
            title="Merge conflicts"
            workers={data.mergeConflicts}
            emptyHint=""
            selectedTaskId={selectedTaskId}
            onSelectTask={onSelectTask}
            onOpenDecision={(w, a) => setDialog({ worker: w, action: a })}
            authoritativeActions={authoritativeActions}
            onPathNotice={reportPathNotice}
          />
          <Section
            title="Completed workers"
            workers={data.completedWorkers}
            emptyHint=""
            selectedTaskId={selectedTaskId}
            onSelectTask={onSelectTask}
            onOpenDecision={(w, a) => setDialog({ worker: w, action: a })}
            authoritativeActions={authoritativeActions}
            onPathNotice={reportPathNotice}
          />
          <Section
            title="Revoked / abandoned"
            workers={data.revokedAbandoned}
            emptyHint=""
            selectedTaskId={selectedTaskId}
            onSelectTask={onSelectTask}
            onOpenDecision={(w, a) => setDialog({ worker: w, action: a })}
            authoritativeActions={authoritativeActions}
            onPathNotice={reportPathNotice}
          />
        </div>
      )}

      {dialog && (
        <WorkerDecisionConfirmDialog
          sessionId={sessionId}
          worker={dialog.worker}
          action={dialog.action}
          open
          onClose={() => setDialog(null)}
          onCompleted={() => void load()}
        />
      )}
    </section>
  );
}
