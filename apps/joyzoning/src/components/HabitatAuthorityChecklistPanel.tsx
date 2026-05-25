"use client";

import { useEffect, useState } from "react";
import {
  fetchHabitatAuthorityChecklist,
  type HabitatAuthorityChecklist,
} from "@/lib/habitat-authority-checklist";

export function HabitatAuthorityChecklistPanel({ compact = false }: { compact?: boolean }) {
  const [checklist, setChecklist] = useState<HabitatAuthorityChecklist | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    const load = async () => {
      try {
        const data = await fetchHabitatAuthorityChecklist();
        if (active) {
          setChecklist(data);
          setError(null);
        }
      } catch (err) {
        if (active) {
          setChecklist(null);
          setError(err instanceof Error ? err.message : "Checklist unavailable");
        }
      }
    };
    void load();
    const interval = setInterval(load, 8000);
    return () => {
      active = false;
      clearInterval(interval);
    };
  }, []);

  if (error) {
    return (
      <p className="text-xs text-rose-400" data-testid="habitat-authority-checklist-error">
        Authority checklist: {error}
      </p>
    );
  }

  if (!checklist) {
    return (
      <p className="text-xs text-zinc-500" data-testid="habitat-authority-checklist-loading">
        Loading authority checklist…
      </p>
    );
  }

  return (
    <div
      data-testid="habitat-authority-checklist"
      className={compact ? "space-y-2" : "rounded-xl border border-zinc-800 bg-zinc-950/50 p-4 space-y-3"}
    >
      {!compact && (
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h3 className="text-xs font-bold uppercase tracking-wider text-zinc-400">
            Authority checklist
          </h3>
          <span
            className={`rounded border px-2 py-0.5 text-[10px] font-semibold uppercase ${
              checklist.allPassed
                ? "border-emerald-500/30 bg-emerald-500/10 text-emerald-300"
                : "border-amber-500/30 bg-amber-500/10 text-amber-200"
            }`}
          >
            {checklist.allPassed ? "Boundaries OK" : "Needs attention"}
          </span>
        </div>
      )}

      <ul className="space-y-2 text-xs">
        {checklist.items.map((item) => (
          <li key={item.id} className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <p className="text-zinc-200">{item.label}</p>
              <p className="text-[10px] text-zinc-500">{item.detail}</p>
            </div>
            <span
              className={`shrink-0 rounded px-1.5 py-0.5 text-[10px] font-medium ${
                item.ok
                  ? "bg-emerald-500/10 text-emerald-300"
                  : "bg-amber-500/10 text-amber-200"
              }`}
            >
              {item.ok ? "OK" : "Fix"}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}
