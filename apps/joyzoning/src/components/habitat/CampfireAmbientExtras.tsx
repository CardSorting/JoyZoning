"use client";

/**
 * Campfire-style ambient visuals — Habitat mode only (non-canonical glance).
 * Formerly stacked on WatchDashboard; must not host merge/kanban authority.
 */

import { useMemo, useState } from "react";
import { AlertTriangle } from "lucide-react";
import type { JoyEvent, LiveTaskSnapshot, StreamLine } from "@/lib/types";
import { ActivityTimeline } from "../ActivityTimeline";
import { DeliverableCards } from "../DeliverableCards";
import { Spotlight } from "../Spotlight";
import { StatusHero } from "../StatusHero";
import { Workshop } from "../Workshop";

type Panel = "live" | "outputs" | "events";

export function CampfireAmbientExtras({
  snapshot,
  stream,
  files,
  newFilePaths,
  events,
}: {
  snapshot: LiveTaskSnapshot;
  stream: StreamLine[];
  files: string[];
  newFilePaths?: Set<string>;
  events: JoyEvent[];
}) {
  const [panel, setPanel] = useState<Panel>("live");
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
    <div
      data-campfire-ambient="true"
      data-canonical-surface="false"
      className="space-y-4 border-t border-pet-elevated/50 pt-4"
    >
      <p className="text-[10px] font-semibold uppercase tracking-widest text-pet-muted">
        Campfire glance
      </p>

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

      <Spotlight text={spotlightText} isActive={isActive} kind={spotlightKind} />

      <div
        className="flex gap-1 rounded-xl border border-campfire-border bg-campfire-elevated p-1"
        role="tablist"
      >
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
          <Workshop stream={stream} files={files} newFilePaths={newFilePaths ?? new Set()} />
        )}
        {panel === "outputs" && <DeliverableCards items={d.deliverables} />}
        {panel === "events" && (
          <ActivityTimeline recentActivity={d.recentActivity} events={events} />
        )}
      </div>
    </div>
  );
}
