"use client";

import { ArrowRight, FolderGit2, GitBranch, Info } from "lucide-react";
import { buildConvergenceModel, formatCommitLine } from "@/lib/convergence";
import type { ConsoleWorker } from "@/lib/operator-console";
import { openPathInShell } from "@/lib/path-actions";
import type { LiveTaskSnapshot } from "@/lib/types";

function PathRow({ label, path }: { label: string; path: string }) {
  return (
    <div className="min-w-0">
      <p className="text-[10px] font-semibold uppercase tracking-wider text-zinc-500">{label}</p>
      <p className="truncate font-mono text-[11px] text-zinc-300" title={path}>
        {path}
      </p>
    </div>
  );
}

export function ConvergencePanel({
  snapshot,
  worker,
  sessionWorkspaceRoot,
  onPathNotice,
}: {
  snapshot: LiveTaskSnapshot;
  worker: ConsoleWorker | null;
  sessionWorkspaceRoot?: string | null;
  onPathNotice?: (message: string, variant: "info" | "error") => void;
}) {
  const model = buildConvergenceModel(snapshot, worker, sessionWorkspaceRoot);

  const openPath = async (path: string | null, label: string) => {
    if (!path) {
      onPathNotice?.(`No ${label} path`, "error");
      return;
    }
    const res = await openPathInShell(path);
    onPathNotice?.(
      res.ok ? `Opened ${label}` : res.error ?? "Could not open path",
      res.ok ? "info" : "error",
    );
  };

  const workerLabel = model.workerWorktreePath ? "Worker worktree" : "Worker mirror";

  return (
    <section
      data-testid="convergence-panel"
      id="operator-section-convergence"
      className="scroll-mt-4 rounded-2xl border border-violet-500/30 bg-violet-950/20 p-4"
    >
      <div className="mb-3 flex flex-wrap items-center gap-2">
        <FolderGit2 className="h-4 w-4 text-violet-400" />
        <h2 className="text-xs font-bold uppercase tracking-widest text-violet-300">
          Convergence
        </h2>
        <span className="rounded-full border border-violet-500/40 bg-violet-500/10 px-2 py-0.5 text-[10px] font-semibold text-violet-200">
          {model.mergeStatus}
        </span>
      </div>

      <p className="mb-3 text-xs text-zinc-400">
        {model.codeEnteredMainWorkspace
          ? "This worker's changes were applied into the main workspace on accept."
          : "This worker's changes live in the worktree below. Accept result converges them into the main workspace before marking Merged."}
      </p>

      {model.codeEnteredMainWorkspace && (
        <p
          className="mb-3 rounded-lg border border-emerald-500/40 bg-emerald-500/10 px-3 py-2 text-xs font-semibold text-emerald-200"
          data-testid="convergence-success"
        >
          Code entered main workspace · {model.convergenceStrategy} ·{" "}
          {formatCommitLine(model.destinationNewHead, null)}
        </p>
      )}

      <div
        className="mb-4 flex flex-wrap items-center justify-center gap-1 rounded-lg border border-zinc-700/80 bg-zinc-900/60 px-2 py-3 font-mono text-[10px] text-zinc-400"
        aria-hidden
      >
        <span className="text-violet-300">Worker worktree</span>
        <ArrowRight className="h-3 w-3 shrink-0" />
        <span>Merge queue</span>
        <ArrowRight className="h-3 w-3 shrink-0" />
        <span>Human review</span>
        <ArrowRight className="h-3 w-3 shrink-0" />
        <span className="text-emerald-300">Main workspace</span>
      </div>

      <div className="mb-4 grid gap-3 sm:grid-cols-2">
        <PathRow label="Main workspace (canonical)" path={model.mainWorkspacePath} />
        <PathRow
          label="Destination on accept (metadata)"
          path={model.destinationWorkspacePath}
        />
        <PathRow
          label={workerLabel}
          path={model.workerWorktreePath ?? model.workerMirrorPath ?? "—"}
        />
        {model.workerMirrorPath && model.workerWorktreePath && (
          <PathRow label="Live mirror" path={model.workerMirrorPath} />
        )}
        <div>
          <p className="text-[10px] font-semibold uppercase tracking-wider text-zinc-500">
            Worker branch
          </p>
          <p className="flex items-center gap-1 font-mono text-[11px] text-zinc-300">
            <GitBranch className="h-3 w-3 text-zinc-500" />
            {model.workerBranch ?? "—"}
          </p>
        </div>
        <div>
          <p className="text-[10px] font-semibold uppercase tracking-wider text-zinc-500">
            Head / base commit
          </p>
          <p className="font-mono text-[11px] text-zinc-300">
            {formatCommitLine(model.headCommit, model.baseCommit)}
          </p>
        </div>
      </div>

      {model.appliedFiles.length > 0 && (
        <div className="mb-3">
          <p className="text-[10px] font-semibold uppercase tracking-wider text-zinc-500">
            Applied to main ({model.appliedFiles.length})
          </p>
          <ul className="mt-1 max-h-24 overflow-y-auto font-mono text-[10px] text-emerald-300/90">
            {model.appliedFiles.slice(0, 12).map((f) => (
              <li key={f} className="truncate">
                {f}
              </li>
            ))}
          </ul>
        </div>
      )}

      {model.changedFilesCount > 0 && (
        <div className="mb-3">
          <p className="text-[10px] font-semibold uppercase tracking-wider text-zinc-500">
            Changed in worktree ({model.changedFilesCount})
          </p>
          <ul className="mt-1 max-h-24 overflow-y-auto font-mono text-[10px] text-zinc-400">
            {model.changedFilesSummary.slice(0, 12).map((f) => (
              <li key={f} className="truncate">
                {f}
              </li>
            ))}
            {model.changedFilesCount > model.changedFilesSummary.length && (
              <li className="text-zinc-600">…and more</li>
            )}
          </ul>
        </div>
      )}

      {model.conflictFiles.length > 0 && (
        <div className="mb-3 rounded-lg border border-red-500/40 bg-red-500/10 px-3 py-2">
          <p className="text-[10px] font-semibold uppercase text-red-300">Conflict files</p>
          <ul className="mt-1 font-mono text-[10px] text-red-200/90">
            {model.conflictFiles.map((f) => (
              <li key={f} className="truncate">
                {f}
              </li>
            ))}
          </ul>
        </div>
      )}

      <div className="mb-3 flex gap-2">
        <button
          type="button"
          data-testid="open-main-workspace"
          onClick={() => void openPath(model.mainWorkspacePath, "main workspace")}
          className="rounded-lg border border-emerald-600/50 bg-emerald-600/15 px-3 py-1.5 text-xs font-semibold text-emerald-200 hover:bg-emerald-600/25"
        >
          Open main workspace
        </button>
        <button
          type="button"
          data-testid="open-worker-workspace"
          onClick={() =>
            void openPath(
              model.workerWorktreePath ?? model.workerMirrorPath,
              "worker workspace",
            )
          }
          disabled={!model.workerWorktreePath && !model.workerMirrorPath}
          className="rounded-lg border border-violet-600/50 bg-violet-600/15 px-3 py-1.5 text-xs font-semibold text-violet-200 hover:bg-violet-600/25 disabled:opacity-40"
        >
          Open worker workspace
        </button>
      </div>

      <div className="space-y-2 rounded-lg border border-zinc-700 bg-zinc-900/50 px-3 py-2 text-[11px] text-zinc-400">
        <p className="flex gap-2">
          <Info className="mt-0.5 h-3.5 w-3.5 shrink-0 text-sky-400" />
          <span>
            <strong className="text-zinc-300">On accept result:</strong> {model.acceptOperation}
          </span>
        </p>
        <p>
          <strong className="text-zinc-300">Ready to merge:</strong> {model.readyToMergeGate}
        </p>
        {model.lastResult && (
          <p className="text-zinc-300">
            <strong>Last result:</strong> {model.lastResult}
          </p>
        )}
        {model.mergeStatusDetail && !model.lastResult && (
          <p>{model.mergeStatusDetail}</p>
        )}
      </div>
    </section>
  );
}
