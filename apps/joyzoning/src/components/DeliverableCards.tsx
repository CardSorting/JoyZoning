"use client";

import { Check } from "lucide-react";
import { cn } from "@/lib/cn";
import type { Deliverable } from "@/lib/types";

export function DeliverableCards({ items }: { items: Deliverable[] }) {
  if (!items.length) {
    return <p className="text-campfire-muted">Deliverables will appear once the build starts.</p>;
  }

  return (
    <ul className="grid gap-3 sm:grid-cols-2">
      {items.map((d) => (
        <li
          key={d.id}
          className={cn(
            "flex items-center gap-3 rounded-xl border p-4 transition-colors",
            d.done
              ? "border-campfire-ok/30 bg-campfire-ok/5"
              : "border-campfire-border bg-campfire-elevated",
          )}
        >
          <span
            className={cn(
              "flex h-9 w-9 shrink-0 items-center justify-center rounded-full",
              d.done ? "bg-campfire-ok text-campfire-bg" : "bg-campfire-border text-campfire-muted",
            )}
          >
            {d.done ? <Check className="h-5 w-5" strokeWidth={3} /> : "○"}
          </span>
          <div>
            <p className="font-medium text-campfire-text">{d.label}</p>
            {d.count != null && d.count > 0 && (
              <p className="text-sm text-campfire-muted">{d.count} items</p>
            )}
          </div>
        </li>
      ))}
    </ul>
  );
}
