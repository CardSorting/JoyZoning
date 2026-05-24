"use client";

import { activityLabel, leaseLabel } from "@/lib/presentation";
import type { JoyEvent, LiveTaskSnapshot, StreamLine } from "@/lib/types";

export function PetObservatory({
  snapshot,
  events,
  stream,
  connLabel,
  files,
  open,
  onOpenChange,
  scrollToStack = false,
}: {
  snapshot: LiveTaskSnapshot;
  events: JoyEvent[];
  stream: StreamLine[];
  connLabel: string;
  files: string[];
  open: boolean;
  onOpenChange: (open: boolean) => void;
  scrollToStack?: boolean;
}) {
  const d = snapshot.display;

  return (
    <div className="pet-panel overflow-hidden">
      <button
        type="button"
        onClick={() => onOpenChange(!open)}
        className="flex w-full items-center justify-between px-4 py-3 text-left text-sm font-semibold text-pet-cream"
        aria-expanded={open}
      >
        <span>🔭 Observatory — stack trace basement</span>
        <span className="text-pet-muted">{open ? "▾" : "▸"}</span>
      </button>

      {open && (
        <div className="space-y-4 border-t border-pet-elevated/60 px-4 pb-4 pt-3 text-sm">
          <p className="text-xs text-pet-muted">
            Real orchestration data. Friendly UI lives upstairs; errors live here.
          </p>

          <section
            id="pet-stack-trace"
            className={
              scrollToStack
                ? "rounded-pet ring-2 ring-pet-rose/50"
                : undefined
            }
          >
            <h3 className="text-xs font-semibold uppercase text-pet-rose">Blocked / exception</h3>
            <pre className="mt-1 max-h-40 overflow-auto rounded-pet bg-pet-night/80 p-3 font-mono text-[11px] text-pet-cream whitespace-pre-wrap">
              {snapshot.blockedReason?.trim() ||
                d?.staleWarning ||
                "No blocked reason on record."}
            </pre>
          </section>

          <dl className="grid gap-3 sm:grid-cols-2">
            <div>
              <dt className="text-xs text-pet-muted">Task id</dt>
              <dd className="break-all font-mono text-[11px] text-pet-cream">{snapshot.taskId}</dd>
            </div>
            <div>
              <dt className="text-xs text-pet-muted">Connection</dt>
              <dd className="text-pet-cream">{connLabel}</dd>
            </div>
            <div>
              <dt className="text-xs text-pet-muted">Lease</dt>
              <dd className="text-pet-cream">{leaseLabel(snapshot.leaseStatus)}</dd>
            </div>
            <div>
              <dt className="text-xs text-pet-muted">Activity</dt>
              <dd className="text-pet-cream">{activityLabel(d?.activityState ?? "none")}</dd>
            </div>
            <div>
              <dt className="text-xs text-pet-muted">Phase</dt>
              <dd className="text-pet-cream">{d?.phaseLabel ?? "—"}</dd>
            </div>
            <div>
              <dt className="text-xs text-pet-muted">Poll</dt>
              <dd className="text-pet-cream">
                {snapshot.recommendedPollSeconds}s · {d?.pollMode}
              </dd>
            </div>
            <div className="sm:col-span-2">
              <dt className="text-xs text-pet-muted">Headline</dt>
              <dd className="text-pet-cream">{d?.headline ?? "—"}</dd>
            </div>
            <div className="sm:col-span-2">
              <dt className="text-xs text-pet-muted">Workspace</dt>
              <dd className="break-all font-mono text-[11px] text-pet-muted">
                {snapshot.worktreePath ?? snapshot.sessionWorkspaceRoot ?? "—"}
              </dd>
            </div>
          </dl>

          <div>
            <h3 className="text-xs font-semibold text-pet-muted">Recent events</h3>
            <ul className="mt-1 max-h-32 space-y-1 overflow-auto rounded-pet bg-pet-night/60 p-2 font-mono text-[10px] text-pet-muted">
              {events.length === 0 && <li>—</li>}
              {events.slice(-16).map((ev, i) => (
                <li key={i} className="truncate">
                  {ev.summary ?? ev.Summary ?? ev.type ?? ev.Type}
                </li>
              ))}
            </ul>
          </div>

          <div>
            <h3 className="text-xs font-semibold text-pet-muted">Live stream</h3>
            <ul className="mt-1 max-h-28 space-y-1 overflow-auto rounded-pet bg-pet-night/60 p-2 font-mono text-[10px] text-pet-muted">
              {stream.length === 0 && <li>—</li>}
              {stream.slice(-12).map((line) => (
                <li key={line.id} className="truncate">
                  [{line.kind}] {line.text}
                </li>
              ))}
            </ul>
          </div>

          {files.length > 0 && (
            <div>
              <h3 className="text-xs font-semibold text-pet-muted">Changed files</h3>
              <ul className="mt-1 max-h-24 space-y-0.5 overflow-auto font-mono text-[10px] text-pet-muted">
                {files.slice(0, 20).map((f) => (
                  <li key={f} className="truncate">
                    {f}
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
