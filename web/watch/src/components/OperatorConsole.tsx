"use client";

/**
 * Single-screen operator console — one task, one worker, one primary action.
 * No mode tabs; server-recommended mode is emphasis + scroll only.
 */

import { useCallback, useMemo, useState } from "react";
import {
  AlertTriangle,
  ExternalLink,
  GitMerge,
  Layers,
  RefreshCw,
  ShieldCheck,
  XCircle,
} from "lucide-react";
import { useOperatorSessionData } from "@/hooks/useOperatorSessionData";
import { useModeEmphasis } from "@/hooks/useModeEmphasis";
import { resolveModeNavigation } from "@/lib/watch-live-binding";
import {
  findWorkerForTask,
  primaryActionForWorker,
  sectionHighlight,
  workspacePathForWorker,
} from "@/lib/operator-console";
import type { MergeWorkerEntry, WorkerMergeState } from "@/lib/merge-queue";
import { mergeStateLabel, mergeStateTone } from "@/lib/merge-queue";
import type { OperatorDecisionAction } from "@/lib/operator-decision";
import { openPathInShell } from "@/lib/path-actions";
import type { ParallelWorkerEntry } from "@/lib/parallel-workers";
import { healthLabel, healthTone } from "@/lib/parallel-workers";
import type { WatchOperatorShellProps } from "./watch-operator-shell-props";
import { ModeEmphasisBar } from "./ModeEmphasisBar";
import { ConnectionPill } from "./ConnectionPill";
import { PipelineFlow } from "./PipelineFlow";
import { ActivityTimeline } from "./ActivityTimeline";
import { KanbanBoard } from "./KanbanBoard";
import { PathActionNotice } from "./PathActionNotice";
import { WorkerDecisionConfirmDialog } from "./WorkerDecisionConfirmDialog";
import { MergeQueuePanel } from "./MergeQueuePanel";
import { ConvergencePanel } from "./ConvergencePanel";
import { AuthorityAutopilotBar, AutopilotActivityFeed } from "./AuthorityAutopilotBar";
import {
  authorityBlockMessage,
  collectAutopilotActivity,
  sessionAuthorityFromSnapshot,
  workerNeedsHumanReview,
} from "@/lib/authority";

function connVariant(connLabel: string): "live" | "error" | "idle" {
  if (connLabel.startsWith("Live")) return "live";
  if (connLabel === "Error") return "error";
  return "idle";
}

function shortId(id: string) {
  return id.length > 12 ? `${id.slice(0, 8)}…` : id;
}

function WorkerRow({
  worker,
  selected,
  onSelect,
}: {
  worker: ParallelWorkerEntry | MergeWorkerEntry;
  selected: boolean;
  onSelect: () => void;
}) {
  const mergeState = "mergeState" in worker ? worker.mergeState : undefined;
  return (
    <button
      type="button"
      onClick={onSelect}
      className={`w-full rounded-lg border px-3 py-2 text-left transition-colors ${
        selected
          ? "border-sky-500/60 bg-sky-500/15"
          : "border-zinc-700 bg-zinc-800/60 hover:border-zinc-600"
      }`}
      data-selected={selected || undefined}
    >
      <p className="truncate text-sm font-semibold text-zinc-100">{worker.taskTitle}</p>
      <p className="mt-0.5 text-[10px] text-zinc-500">
        {worker.leaseStatus}
        {mergeState ? ` · ${mergeStateLabel(mergeState as WorkerMergeState)}` : ""}
      </p>
    </button>
  );
}

export function OperatorConsole({
  snapshot,
  sessionId,
  boardTasks,
  events,
  stream,
  connLabel,
  onNudge,
  onDepart,
  onSelectTask,
}: WatchOperatorShellProps) {
  const modeNav = resolveModeNavigation(snapshot);
  const { emphasis, setEmphasis } = useModeEmphasis(modeNav.recommendedMode);
  const { workers, mergeQueue, loading, error, refresh } = useOperatorSessionData(sessionId);

  const [dialog, setDialog] = useState<{
    worker: MergeWorkerEntry;
    action: OperatorDecisionAction;
  } | null>(null);
  const [pathNotice, setPathNotice] = useState<{
    message: string;
    variant: "info" | "error";
  } | null>(null);

  const taskId = snapshot.taskId;
  const allMergeWorkers = mergeQueue?.allWorkers ?? [
    ...(mergeQueue?.readyToMerge ?? []),
    ...(mergeQueue?.mergeConflicts ?? []),
    ...(mergeQueue?.completedWorkers ?? []),
    ...(mergeQueue?.revokedAbandoned ?? []),
  ];

  const parallelList = workers?.workers ?? [];
  const selectedWorker = useMemo(
    () => findWorkerForTask(taskId, allMergeWorkers, parallelList),
    [taskId, allMergeWorkers, parallelList],
  );

  const selectedMerge = useMemo(() => {
    if (!selectedWorker || !("mergeState" in selectedWorker)) return null;
    return selectedWorker as MergeWorkerEntry;
  }, [selectedWorker]);

  const primary = primaryActionForWorker(selectedMerge ?? selectedWorker, snapshot.leaseStatus);

  const reportPath = useCallback((message: string | null, variant: "info" | "error" = "info") => {
    if (!message) {
      setPathNotice(null);
      return;
    }
    setPathNotice({ message, variant });
    window.setTimeout(() => setPathNotice(null), 3500);
  }, []);

  const runPrimary = useCallback(async () => {
    if (primary.action === "refresh") {
      onNudge();
      void refresh();
      return;
    }
    if (primary.action === "open_workspace") {
      const path = workspacePathForWorker(selectedWorker);
      if (!path) {
        reportPath("No workspace path on this worker", "error");
        return;
      }
      const res = await openPathInShell(path);
      reportPath(res.ok ? "Opened workspace in Finder" : res.error, res.ok ? "info" : "error");
      return;
    }
    if (
      selectedMerge &&
      (primary.action === "approve" || primary.action === "inspect" || primary.action === "revoke")
    ) {
      setDialog({ worker: selectedMerge, action: primary.action });
    }
  }, [primary, selectedWorker, selectedMerge, onNudge, refresh, reportPath]);

  const workerList = useMemo(() => {
    const byTask = new Map<string, ParallelWorkerEntry | MergeWorkerEntry>();
    for (const w of parallelList) byTask.set(w.taskId.toLowerCase(), w);
    for (const w of allMergeWorkers) byTask.set(w.taskId.toLowerCase(), w);
    return [...byTask.values()];
  }, [parallelList, allMergeWorkers]);

  const sessionAuthority = useMemo(() => sessionAuthorityFromSnapshot(workers), [workers]);
  const autopilotActivity = useMemo(() => collectAutopilotActivity(workerList), [workerList]);
  const needsReviewWorkers = useMemo(
    () => workerList.filter((w) => workerNeedsHumanReview(w)),
    [workerList],
  );

  return (
    <div
      data-testid="operator-console"
      data-joyzoning-canonical-shell="operator-console"
      className="mx-auto max-w-4xl space-y-4 pb-10 text-zinc-100"
    >
      <header className="flex flex-wrap items-start justify-between gap-3 border-b border-zinc-800 pb-4">
        <div className="min-w-0 flex-1 space-y-1">
          <p className="text-[10px] font-semibold uppercase tracking-widest text-zinc-500">
            JoyZoning operator
          </p>
          <h1 className="text-lg font-bold leading-snug">
            {snapshot.title || snapshot.display?.headline}
          </h1>
          <p className="text-sm text-zinc-400">
            {snapshot.display?.headline} · Lease{" "}
            <span className="font-mono text-zinc-300">{snapshot.leaseStatus}</span>
          </p>
          <p className="truncate font-mono text-[11px] text-zinc-500">
            {snapshot.sessionWorkspaceRoot}
          </p>
          {snapshot.blockedReason && (
            <p className="flex gap-2 rounded-lg border border-red-500/40 bg-red-500/10 px-3 py-2 text-xs text-red-200">
              <AlertTriangle className="h-4 w-4 shrink-0" />
              {snapshot.blockedReason}
            </p>
          )}
        </div>
        <div className="flex flex-col items-end gap-2">
          <ConnectionPill label={connLabel} variant={connVariant(connLabel)} />
          <button
            type="button"
            onClick={onDepart}
            className="rounded-lg border border-zinc-600 px-3 py-1.5 text-xs text-zinc-400 hover:text-zinc-200"
          >
            Leave
          </button>
        </div>
      </header>

      <div className="flex flex-wrap items-center gap-3">
        <button
          type="button"
          data-testid="primary-action"
          onClick={() => void runPrimary()}
          disabled={primary.kind === "none"}
          className="inline-flex items-center gap-2 rounded-xl bg-sky-600 px-4 py-2.5 text-sm font-bold text-white shadow-lg hover:bg-sky-500 disabled:opacity-40"
        >
          {primary.kind === "approve" && <ShieldCheck className="h-4 w-4" />}
          {primary.kind === "conflict" && <AlertTriangle className="h-4 w-4" />}
          {primary.kind === "open" && <ExternalLink className="h-4 w-4" />}
          {primary.kind === "refresh" && <RefreshCw className="h-4 w-4" />}
          {primary.label}
        </button>
        <button
          type="button"
          onClick={() => {
            onNudge();
            void refresh();
          }}
          className="inline-flex items-center gap-1 rounded-lg border border-zinc-600 px-3 py-2 text-xs text-zinc-400 hover:text-zinc-200"
        >
          <RefreshCw className={`h-3.5 w-3.5 ${loading ? "animate-spin" : ""}`} />
          Refresh
        </button>
        {modeNav.inferred && (
          <span className="text-[10px] text-zinc-500">Mode hints inferred from lease status</span>
        )}
      </div>

      <AuthorityAutopilotBar
        authority={sessionAuthority}
        autoAcceptedCount={autopilotActivity.accepted.length}
        needsReviewCount={needsReviewWorkers.length}
        sessionId={sessionId}
        onReconciled={refresh}
      />

      <AutopilotActivityFeed
        accepted={autopilotActivity.accepted}
        blocked={autopilotActivity.blocked}
      />

      <ModeEmphasisBar
        emphasis={emphasis}
        recommendedMode={modeNav.recommendedMode}
        onEmphasis={setEmphasis}
      />

      <PathActionNotice message={pathNotice?.message ?? null} variant={pathNotice?.variant ?? "info"} />

      <ConvergencePanel
        snapshot={snapshot}
        worker={selectedWorker}
        sessionWorkspaceRoot={mergeQueue?.sessionWorkspaceRoot ?? workers?.sessionWorkspaceRoot}
        onPathNotice={(message, variant) => reportPath(message, variant)}
      />

      {error && (
        <p className="text-xs text-red-300" role="alert">
          {error}
        </p>
      )}

      <section
        id="operator-section-execution"
        className={`scroll-mt-4 rounded-2xl border border-zinc-700 bg-zinc-900/50 p-4 ${sectionHighlight(emphasis, "execution")}`}
      >
        <h2 className="mb-3 text-xs font-bold uppercase tracking-widest text-zinc-500">
          Status & pipeline
        </h2>
        <PipelineFlow snapshot={snapshot} />
      </section>

      <div className="grid gap-4 lg:grid-cols-[minmax(200px,260px)_1fr]">
        <section
          id="operator-section-execution-workers"
          className={`scroll-mt-4 space-y-2 rounded-2xl border border-zinc-700 bg-zinc-900/50 p-4 ${sectionHighlight(emphasis, "execution")}`}
        >
          <div className="mb-2 flex items-center gap-2">
            <Layers className="h-4 w-4 text-zinc-500" />
            <h2 className="text-xs font-bold uppercase tracking-widest text-zinc-500">
              Workers ({workerList.length})
            </h2>
          </div>
          {workerList.length === 0 && !loading && (
            <p className="text-xs text-zinc-500">No workers on this session.</p>
          )}
          <div className="space-y-1.5" data-testid="worker-list">
            {workerList.map((w) => (
              <WorkerRow
                key={w.leaseId}
                worker={w}
                selected={w.taskId.toLowerCase() === taskId.toLowerCase()}
                onSelect={() => onSelectTask?.(w.taskId)}
              />
            ))}
          </div>
        </section>

        <section
          data-testid="selected-worker-detail"
          className="scroll-mt-4 rounded-2xl border border-zinc-700 bg-zinc-900/50 p-4"
        >
          <h2 className="mb-3 text-xs font-bold uppercase tracking-widest text-zinc-500">
            Selected worker
          </h2>
          {!selectedWorker && (
            <p className="text-sm text-zinc-500">Select a worker from the list.</p>
          )}
          {selectedWorker && (
            <div className="space-y-3">
              <div className="flex flex-wrap items-start justify-between gap-2">
                <div>
                  <p className="font-semibold">{selectedWorker.taskTitle}</p>
                  <p className="text-[11px] text-zinc-500">
                    Lease {shortId(selectedWorker.leaseId)} · {selectedWorker.leaseStatus}
                  </p>
                </div>
                <span
                  className={`rounded-full border px-2 py-0.5 text-[10px] font-semibold uppercase ${healthTone(selectedWorker.healthState)}`}
                >
                  {healthLabel(selectedWorker.healthState)}
                </span>
              </div>

              {"mergeState" in selectedWorker && (
                <p
                  className={`inline-block rounded-full border px-2 py-0.5 text-[10px] font-semibold uppercase ${mergeStateTone(selectedWorker.mergeState as WorkerMergeState)}`}
                >
                  {mergeStateLabel(selectedWorker.mergeState as WorkerMergeState)}
                </p>
              )}

              {selectedMerge?.mergeReadiness && (
                <div className="space-y-1 text-[11px] text-zinc-400">
                  <p>
                    {selectedMerge.mergeReadiness.changedFilesCount} changed file(s)
                    {selectedMerge.mergeReadiness.verificationSummary
                      ? ` · ${selectedMerge.mergeReadiness.verificationSummary}`
                      : ""}
                  </p>
                  {selectedMerge.mergeReadiness.changedFilesSummary.length > 0 && (
                    <p className="font-mono text-zinc-300">
                      {selectedMerge.mergeReadiness.changedFilesSummary.join(", ")}
                    </p>
                  )}
                </div>
              )}

              {selectedWorker.authority && (
                <div
                  className={`rounded-lg border px-2 py-2 text-xs ${
                    selectedWorker.authority.wasAutoAccepted
                      ? "border-emerald-500/40 bg-emerald-500/10 text-emerald-200"
                      : selectedWorker.authority.needsHumanReview
                        ? "border-amber-500/40 bg-amber-500/10 text-amber-100"
                        : "border-zinc-600 bg-zinc-800/50 text-zinc-300"
                  }`}
                >
                  {selectedWorker.authority.wasAutoAccepted ? (
                    <p>Auto-accepted by {sessionAuthority.profileLabel}</p>
                  ) : selectedWorker.authority.needsHumanReview ? (
                    <p>{authorityBlockMessage(selectedWorker) ?? "Needs human review"}</p>
                  ) : (
                    <p>Autopilot eligible — no manual review required</p>
                  )}
                </div>
              )}

              {selectedMerge?.mergeConflict && (
                <div className="rounded-lg border border-red-500/30 bg-red-500/10 p-2 text-xs text-red-200">
                  <p className="font-semibold">{selectedMerge.mergeConflict.category}</p>
                  <p>{selectedMerge.mergeConflict.reason}</p>
                  {selectedMerge.mergeConflict.conflictFiles.length > 0 && (
                    <p className="mt-1 font-mono text-[10px]">
                      {selectedMerge.mergeConflict.conflictFiles.join(", ")}
                    </p>
                  )}
                </div>
              )}

              {selectedMerge?.decisionSummary?.riskFlags.active &&
                selectedMerge.decisionSummary.riskFlags.active.length > 0 && (
                  <p className="text-[11px] text-amber-300">
                    Risks before accept:{" "}
                    {selectedMerge.decisionSummary.riskFlags.active.join(" · ")}
                  </p>
                )}

              <div className="flex flex-wrap gap-2 pt-2">
                {selectedMerge?.mergeState === "ready_to_merge" &&
                  workerNeedsHumanReview(selectedMerge) && (
                  <button
                    type="button"
                    className="inline-flex items-center gap-1 rounded-lg border border-emerald-500/50 bg-emerald-500/10 px-3 py-1.5 text-xs font-semibold text-emerald-300"
                    onClick={() => setDialog({ worker: selectedMerge, action: "approve" })}
                  >
                    <ShieldCheck className="h-3.5 w-3.5" /> Accept
                  </button>
                )}
                {selectedMerge &&
                  (selectedMerge.mergeState === "merge_conflict" ||
                    selectedMerge.mergeState === "merge_failed") && (
                    <button
                      type="button"
                      className="inline-flex items-center gap-1 rounded-lg border border-red-500/50 bg-red-500/10 px-3 py-1.5 text-xs font-semibold text-red-300"
                      onClick={() => setDialog({ worker: selectedMerge, action: "inspect" })}
                    >
                      <AlertTriangle className="h-3.5 w-3.5" /> Review conflict
                    </button>
                  )}
                {selectedMerge &&
                  selectedMerge.mergeState !== "merged" &&
                  selectedMerge.mergeState !== "revoked" && (
                    <button
                      type="button"
                      className="inline-flex items-center gap-1 rounded-lg border border-orange-500/50 bg-orange-500/10 px-3 py-1.5 text-xs font-semibold text-orange-300"
                      onClick={() => setDialog({ worker: selectedMerge, action: "revoke" })}
                    >
                      <XCircle className="h-3.5 w-3.5" /> Revoke
                    </button>
                  )}
                <button
                  type="button"
                  className="inline-flex items-center gap-1 rounded-lg border border-zinc-600 px-3 py-1.5 text-xs text-zinc-300"
                  onClick={() => void runPrimary()}
                >
                  <ExternalLink className="h-3.5 w-3.5" /> Open workspace
                </button>
              </div>
            </div>
          )}
        </section>
      </div>

      <section
        id="operator-section-review"
        className={`scroll-mt-4 ${sectionHighlight(emphasis, "review")}`}
      >
        {sessionId && (
          <MergeQueuePanel
            sessionId={sessionId}
            selectedTaskId={taskId}
            onSelectTask={onSelectTask}
            authoritativeActions
            embedded
            externalSnapshot={mergeQueue}
            onExternalRefresh={refresh}
          />
        )}
      </section>

      <section
        id="operator-section-planning"
        className={`scroll-mt-4 rounded-2xl border border-zinc-700 bg-zinc-900/50 p-4 ${sectionHighlight(emphasis, "planning")}`}
      >
        <h2 className="mb-3 text-xs font-bold uppercase tracking-widest text-zinc-500">
          Board context
        </h2>
        <KanbanBoard tasks={boardTasks} activeTaskId={taskId} onSelectTask={onSelectTask} />
      </section>

      <section className="scroll-mt-4 rounded-2xl border border-zinc-700 bg-zinc-900/50 p-4">
        <h2 className="mb-3 flex items-center gap-2 text-xs font-bold uppercase tracking-widest text-zinc-500">
          <GitMerge className="h-4 w-4" /> Events & stream
        </h2>
        <ActivityTimeline
          events={events}
          recentActivity={snapshot.display?.recentActivity ?? []}
        />
        {stream.length > 0 && (
          <ul className="mt-3 space-y-1 border-t border-zinc-800 pt-3">
            {stream.slice(-8).map((line, i) => (
              <li key={`${line.id}-${i}`} className="font-mono text-[10px] text-zinc-500">
                {line.text}
              </li>
            ))}
          </ul>
        )}
      </section>

      {dialog && sessionId && (
        <WorkerDecisionConfirmDialog
          sessionId={sessionId}
          worker={dialog.worker}
          action={dialog.action}
          open
          onClose={() => setDialog(null)}
          onCompleted={() => void refresh()}
        />
      )}
    </div>
  );
}
