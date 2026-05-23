"use client";

import { useMemo, useState } from "react";
import { AlertTriangle, ChevronDown, RefreshCw } from "lucide-react";
import type { LiveTaskSnapshot, JoyEvent, StreamLine } from "@/lib/types";
import type { BoardTask } from "@/lib/kanban";
import { ActivityTimeline } from "./ActivityTimeline";
import { DeliverableCards } from "./DeliverableCards";
import { KanbanBoard } from "./KanbanBoard";
import { PipelineFlow } from "./PipelineFlow";
import { Spotlight } from "./Spotlight";
import { StatusHero } from "./StatusHero";
import { Workshop } from "./Workshop";

type Panel = "live" | "outputs" | "events";

export function WatchDashboard({
  snapshot,
  stream,
  files,
  newFilePaths,
  events,
  boardTasks,
  onRefresh,
  onSwitchTask,
  onSelectTask,
}: {
  snapshot: LiveTaskSnapshot;
  stream: StreamLine[];
  files: string[];
  newFilePaths: Set<string>;
  events: JoyEvent[];
  boardTasks: BoardTask[];
  onRefresh: () => void;
  onSwitchTask: () => void;
  onSelectTask?: (taskId: string) => void;
}) {
  const [panel, setPanel] = useState<Panel>("live");
  const [showDetails, setShowDetails] = useState(false);
  const d = snapshot.display;
  const act = d.activityState || "waiting";
  const isActive = act === "active" || snapshot.leaseStatus === "Running";
  const isBlocked = Boolean(snapshot.blockedReason) || act === "blocked";

  const { spotlightText, spotlightKind } = useMemo(() => {
    const lastFile = [...stream].reverse().find((l) => l.kind === "file");
    if (lastFile) return { spotlightText: lastFile.text, spotlightKind: "file" as const };
    const lastTool = [...stream].reverse().find((l) => l.kind === "tool");
    if (lastTool) return { spotlightText: lastTool.text, spotlightKind: "tool" as const };
    if (d.currentStepTitle)
      return { spotlightText: d.currentStepTitle, spotlightKind: "idle" as const };
    return {
      spotlightText: isActive ? "Building in worktree…" : "Waiting for worker…",
      spotlightKind: "idle" as const,
    };
  }, [stream, d.currentStepTitle, isActive]);

  const panels: { id: Panel; label: string; icon: string }[] = [
    { id: "live", label: "Live", icon: "⚡" },
    { id: "outputs", label: "Outputs", icon: "📦" },
    { id: "events", label: "Handoffs", icon: "↔" },
  ];

  return (
    <div className="space-y-5 animate-slide-up">
      <StatusHero snapshot={snapshot} />

      {isBlocked && (
        <div
          className="flex gap-3 rounded-xl border border-campfire-err/40 bg-campfire-err/10 p-4 text-campfire-err"
          role="alert"
        >
          <AlertTriangle className="h-5 w-5 shrink-0" />
          <div className="min-w-0">
            <p className="font-semibold">Blocked on kanban</p>
            <p className="mt-1 line-clamp-3 text-sm opacity-90">
              {snapshot.blockedReason || "Fix the issue, then recover and re-run."}
            </p>
          </div>
        </div>
      )}

      <div className="space-y-2">
        <h3 className="text-xs font-bold uppercase tracking-widest text-campfire-muted">
          Session board
        </h3>
        <KanbanBoard
          tasks={boardTasks}
          activeTaskId={snapshot.taskId}
          onSelectTask={onSelectTask}
        />
      </div>

      <PipelineFlow snapshot={snapshot} />

      <Spotlight text={spotlightText} isActive={isActive} kind={spotlightKind} />

      <div className="flex gap-1 rounded-xl border border-campfire-border bg-campfire-elevated p-1" role="tablist">
        {panels.map((p) => (
          <button
            key={p.id}
            type="button"
            role="tab"
            aria-selected={panel === p.id}
            onClick={() => setPanel(p.id)}
            className={`flex flex-1 items-center justify-center gap-1.5 rounded-lg px-3 py-2 text-sm font-semibold transition-colors ${
              panel === p.id
                ? "bg-campfire-accent text-campfire-bg"
                : "text-campfire-muted hover:text-campfire-text"
            }`}
          >
            <span aria-hidden>{p.icon}</span>
            {p.label}
          </button>
        ))}
      </div>

      <div className="rounded-2xl border border-campfire-border bg-campfire-surface p-4">
        {panel === "live" && (
          <Workshop stream={stream} files={files} newFilePaths={newFilePaths} />
        )}
        {panel === "outputs" && <DeliverableCards items={d.deliverables} />}
        {panel === "events" && (
          <ActivityTimeline recentActivity={d.recentActivity} events={events} />
        )}
      </div>

      <div className="flex flex-wrap gap-3">
        <button
          type="button"
          onClick={onRefresh}
          className="inline-flex items-center gap-2 rounded-xl border border-campfire-border bg-campfire-elevated px-4 py-2 text-sm font-medium text-campfire-text hover:border-campfire-accent"
        >
          <RefreshCw className="h-4 w-4" />
          Refresh
        </button>
        <button
          type="button"
          onClick={onSwitchTask}
          className="rounded-xl px-4 py-2 text-sm text-campfire-muted hover:text-campfire-text"
        >
          Switch task
        </button>
      </div>

      {(d.nextActions.length > 0 || d.helpTips.length > 0 || d.staleWarning || d.timeGuidance) && (
        <button
          type="button"
          onClick={() => setShowDetails((v) => !v)}
          className="flex w-full items-center justify-between rounded-xl border border-campfire-border bg-campfire-surface/50 px-4 py-3 text-sm text-campfire-muted hover:text-campfire-text"
        >
          <span>Details & guidance</span>
          <ChevronDown
            className={`h-4 w-4 transition-transform ${showDetails ? "rotate-180" : ""}`}
          />
        </button>
      )}

      {showDetails && (
        <div className="space-y-3 rounded-xl border border-campfire-border bg-campfire-surface/50 p-4 text-sm">
          {d.staleWarning && (
            <p className="text-campfire-warn">{d.staleWarning}</p>
          )}
          {d.timeGuidance && (
            <p className="text-campfire-muted">{d.timeGuidance}</p>
          )}
          {d.nextActions.length > 0 && (
            <ul className="list-disc space-y-1 pl-5 text-campfire-text">
              {d.nextActions.map((a, i) => (
                <li key={i}>{a}</li>
              ))}
            </ul>
          )}
          {d.helpTips.map((tip, i) => (
            <p key={i} className="text-campfire-muted">
              {tip}
            </p>
          ))}
        </div>
      )}
    </div>
  );
}
