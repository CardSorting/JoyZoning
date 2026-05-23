"use client";

import type { JoyEvent, LiveTaskSnapshot } from "@/lib/types";

export function ObservatoryPanel({
  snapshot,
  events,
  connLabel,
}: {
  snapshot: LiveTaskSnapshot;
  events: JoyEvent[];
  connLabel: string;
}) {
  const d = snapshot.display;

  return (
    <div className="cozy-panel space-y-4 p-4 text-sm">
      <div className="rounded-cozy bg-cozy-deep/50 p-3">
        <p className="text-xs font-semibold text-cozy-peach">🔭 Observatory mode</p>
        <p className="mt-1 text-xs text-cozy-muted">
          For villagers who want a peek behind the cozy curtain. Optional only.
        </p>
      </div>
      <dl className="grid gap-3 sm:grid-cols-2">
        <div>
          <dt className="text-xs text-cozy-muted">Synthesis id</dt>
          <dd className="break-all font-mono text-xs text-cozy-cream">{snapshot.taskId}</dd>
        </div>
        <div>
          <dt className="text-xs text-cozy-muted">Village link</dt>
          <dd className="text-cozy-cream">{connLabel}</dd>
        </div>
        <div>
          <dt className="text-xs text-cozy-muted">Growth phase</dt>
          <dd className="text-cozy-cream">{d?.phaseLabel ?? "—"}</dd>
        </div>
        <div>
          <dt className="text-xs text-cozy-muted">Mood</dt>
          <dd className="text-cozy-cream">{d?.activityState ?? "—"}</dd>
        </div>
        <div>
          <dt className="text-xs text-cozy-muted">Rhythm</dt>
          <dd className="text-cozy-cream">
            {snapshot.recommendedPollSeconds}s · {d?.pollMode}
          </dd>
        </div>
        <div>
          <dt className="text-xs text-cozy-muted">Last heartbeat</dt>
          <dd className="text-cozy-cream">
            {snapshot.updatedAt
              ? new Date(snapshot.updatedAt).toLocaleString()
              : "—"}
          </dd>
        </div>
      </dl>
      <div>
        <p className="mb-2 text-xs text-cozy-muted">Recent signals</p>
        <ul className="max-h-36 space-y-1 overflow-auto rounded-cozy bg-cozy-deep/40 p-2 font-mono text-[10px] text-cozy-muted">
          {events.length === 0 && <li>Quiet for now.</li>}
          {events.slice(-14).map((ev, i) => (
            <li key={i} className="truncate">
              {ev.summary ?? ev.Summary ?? ev.type ?? ev.Type ?? "…"}
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}
