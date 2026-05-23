"use client";

import { useCallback, useEffect, useState } from "react";
import { AlertTriangle, Copy, ExternalLink, X } from "lucide-react";
import { api } from "@/lib/api";
import type { MergeWorkerEntry } from "@/lib/merge-queue";
import {
  RISK_FLAG_LABELS,
  type OperatorActionGuardrails,
  type OperatorDecisionAction,
  type OperatorDecisionSummary,
} from "@/lib/operator-decision";
import { shortCommit } from "@/lib/merge-queue";
import { ACCEPT_RESULT_LABEL } from "@/lib/operator-labels";
import { copyPath, openPathInShell } from "@/lib/path-actions";

function RiskFlags({ flags }: { flags: OperatorDecisionSummary["riskFlags"] }) {
  if (!flags.active?.length) {
    return <p className="text-xs text-campfire-muted">No risk flags</p>;
  }

  return (
    <ul className="flex flex-wrap gap-1.5">
      {flags.active.map((key) => (
        <li
          key={key}
          className="rounded-full border border-amber-500/40 bg-amber-500/10 px-2 py-0.5 text-[10px] font-semibold text-amber-200"
        >
          {RISK_FLAG_LABELS[key] ?? key}
        </li>
      ))}
    </ul>
  );
}

function DecisionSummaryBody({
  summary,
  onPathError,
}: {
  summary: OperatorDecisionSummary;
  onPathError: (message: string) => void;
}) {
  return (
    <dl className="grid gap-2 text-sm">
      <div className="flex justify-between gap-2">
        <dt className="text-campfire-muted">Merge state</dt>
        <dd className="font-mono text-campfire-text">{summary.mergeState}</dd>
      </div>
      <div className="flex justify-between gap-2">
        <dt className="text-campfire-muted">Changed files</dt>
        <dd className="text-campfire-text">{summary.changedFilesCount}</dd>
      </div>
      {summary.changedFilesSummary.length > 0 && (
        <div>
          <dt className="text-campfire-muted">Summary</dt>
          <dd className="mt-1 line-clamp-3 font-mono text-[11px] text-campfire-text">
            {summary.changedFilesSummary.join(", ")}
          </dd>
        </div>
      )}
      <div className="flex justify-between gap-2">
        <dt className="text-campfire-muted">Verification</dt>
        <dd className="text-campfire-text">{summary.verificationStatus}</dd>
      </div>
      <div className="flex justify-between gap-2">
        <dt className="text-campfire-muted">Conflict</dt>
        <dd className="text-right text-campfire-text">{summary.conflictStatus}</dd>
      </div>
      {(summary.baseCommit || summary.headCommit) && (
        <div className="font-mono text-[11px] text-campfire-muted">
          base {shortCommit(summary.baseCommit)} → head {shortCommit(summary.headCommit)}
        </div>
      )}
      {summary.worktreePath && (
        <div className="flex flex-wrap items-center gap-2">
          <span className="truncate font-mono text-[10px] text-campfire-muted">{summary.worktreePath}</span>
          <button
            type="button"
            className="inline-flex items-center gap-1 text-[10px] text-campfire-accent"
            onClick={() => {
              void copyPath(summary.worktreePath!).then((res) => {
                if (!res.ok) onPathError(res.error);
              });
            }}
          >
            <Copy className="h-3 w-3" /> Copy
          </button>
          <button
            type="button"
            className="inline-flex items-center gap-1 text-[10px] text-campfire-accent"
            onClick={() => {
              void openPathInShell(summary.worktreePath!).then((res) => {
                if (!res.ok) onPathError(res.error);
              });
            }}
          >
            <ExternalLink className="h-3 w-3" /> Open workspace
          </button>
        </div>
      )}
      {summary.liveMirrorPath && (
        <div className="flex flex-wrap items-center gap-2">
          <span className="truncate font-mono text-[10px] text-campfire-muted">{summary.liveMirrorPath}</span>
          <button
            type="button"
            className="inline-flex items-center gap-1 text-[10px] text-campfire-accent"
            onClick={() => {
              void openPathInShell(summary.liveMirrorPath!).then((res) => {
                if (!res.ok) onPathError(res.error);
              });
            }}
          >
            <ExternalLink className="h-3 w-3" /> Open workspace
          </button>
        </div>
      )}
    </dl>
  );
}

function GuardrailMessages({ guardrails }: { guardrails: OperatorActionGuardrails }) {
  return (
    <div className="space-y-2">
      {guardrails.blockReasons.map((msg) => (
        <p
          key={msg}
          className="flex gap-2 rounded-lg border border-red-500/40 bg-red-500/10 p-2 text-xs text-red-200"
        >
          <AlertTriangle className="h-4 w-4 shrink-0" />
          {msg}
        </p>
      ))}
      {guardrails.warnings.map((msg) => (
        <p
          key={msg}
          className="flex gap-2 rounded-lg border border-amber-500/40 bg-amber-500/10 p-2 text-xs text-amber-100"
        >
          <AlertTriangle className="h-4 w-4 shrink-0" />
          {msg}
        </p>
      ))}
    </div>
  );
}

const TITLES: Record<OperatorDecisionAction, string> = {
  approve: ACCEPT_RESULT_LABEL,
  revoke: "Revoke worker",
  inspect: "Review conflict",
};

export function WorkerDecisionConfirmDialog({
  sessionId,
  worker,
  action,
  open,
  onClose,
  onCompleted,
}: {
  sessionId: string;
  worker: MergeWorkerEntry;
  action: OperatorDecisionAction;
  open: boolean;
  onClose: () => void;
  onCompleted?: () => void;
}) {
  const [acknowledged, setAcknowledged] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [guardrails, setGuardrails] = useState<OperatorActionGuardrails | null>(null);
  const [summary, setSummary] = useState<OperatorDecisionSummary | null>(
    worker.decisionSummary ?? null,
  );

  const loadPreflight = useCallback(async () => {
    if (!worker.executionSessionId) return;
    try {
      const pre = await api.decisionPreflight(sessionId, worker.executionSessionId, action);
      setSummary(pre.decisionSummary);
      setGuardrails(pre.guardrails);
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Preflight failed");
      setGuardrails(
        action === "approve"
          ? worker.approveGuardrails ?? null
          : action === "revoke"
            ? worker.revokeGuardrails ?? null
            : null,
      );
      setSummary(worker.decisionSummary ?? null);
    }
  }, [sessionId, worker, action]);

  useEffect(() => {
    if (!open) return;
    setAcknowledged(false);
    setError(null);
    setGuardrails(
      action === "approve"
        ? worker.approveGuardrails ?? null
        : action === "revoke"
          ? worker.revokeGuardrails ?? null
          : null,
    );
    setSummary(worker.decisionSummary ?? null);
    void loadPreflight();
  }, [open, loadPreflight, worker, action]);

  if (!open) return null;

  const g =
    guardrails ??
    (action === "approve"
      ? worker.approveGuardrails
      : action === "revoke"
        ? worker.revokeGuardrails
        : null);
  const s = summary ?? worker.decisionSummary;
  const canConfirm =
    action === "inspect" ||
    (action === "revoke" && (!g?.requiresAcknowledgement || acknowledged)) ||
    (action === "approve" && !g?.blocked && (!g?.requiresAcknowledgement || acknowledged));

  async function onConfirm() {
    if (!canConfirm) return;
    if (action === "inspect") {
      const path = s?.worktreePath ?? s?.liveMirrorPath ?? worker.liveMirrorPath;
      if (path) {
        const res = await openPathInShell(path);
        if (!res.ok) {
          setError(res.error);
          return;
        }
      }
      onClose();
      return;
    }

    setBusy(true);
    setError(null);
    try {
      if (action === "approve") {
        await api.approveMerge(worker.taskId);
      } else {
        await api.revokeLease(worker.taskId);
      }
      onCompleted?.();
      onClose();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Action failed");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-end justify-center bg-black/60 p-4 sm:items-center"
      role="dialog"
      aria-modal="true"
      aria-labelledby="worker-decision-title"
    >
      <div className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-2xl border border-campfire-border bg-campfire-bg p-5 shadow-xl">
        <div className="mb-4 flex items-start justify-between gap-2">
          <div>
            <h2 id="worker-decision-title" className="text-lg font-bold text-campfire-text">
              {TITLES[action]}
            </h2>
            <p className="text-sm text-campfire-muted">{worker.taskTitle}</p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg p-1 text-campfire-muted hover:bg-campfire-elevated"
            aria-label="Close"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {s && (
          <>
            <h3 className="mb-2 text-xs font-bold uppercase tracking-widest text-campfire-muted">
              Decision summary
            </h3>
            <DecisionSummaryBody summary={s} onPathError={(msg) => setError(msg)} />
            <h3 className="mb-2 mt-4 text-xs font-bold uppercase tracking-widest text-campfire-muted">
              Risk flags
            </h3>
            <RiskFlags flags={s.riskFlags} />
          </>
        )}

        {g && (
          <div className="mt-4">
            <GuardrailMessages guardrails={g} />
          </div>
        )}

        {g?.requiresAcknowledgement && action !== "inspect" && (
          <label className="mt-4 flex cursor-pointer items-start gap-2 text-sm text-campfire-text">
            <input
              type="checkbox"
              checked={acknowledged}
              onChange={(e) => setAcknowledged(e.target.checked)}
              className="mt-1"
            />
            I have reviewed the evidence above and understand the risks.
          </label>
        )}

        {error && <p className="mt-3 text-xs text-red-300">{error}</p>}

        <div className="mt-5 flex flex-wrap justify-end gap-2">
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg border border-campfire-border px-4 py-2 text-sm font-semibold text-campfire-muted"
          >
            Cancel
          </button>
          <button
            type="button"
            disabled={!canConfirm || busy}
            onClick={() => void onConfirm()}
            className={`rounded-lg px-4 py-2 text-sm font-semibold ${
              action === "revoke"
                ? "bg-orange-600 text-white disabled:opacity-40"
                : action === "approve"
                  ? "bg-emerald-600 text-white disabled:opacity-40"
                  : "bg-campfire-accent text-campfire-bg disabled:opacity-40"
            }`}
          >
            {busy ? "Working…" : action === "inspect" ? "Close" : TITLES[action]}
          </button>
        </div>
      </div>
    </div>
  );
}
