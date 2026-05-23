"use client";

import { cn } from "@/lib/cn";
import type { ChatAction } from "@/lib/chat-types";
import { statusCardLabel, statusCardTone } from "@/lib/chat-types";

export function StatusCard({
  kind,
  title,
  content,
  actions,
  onAction,
  busyActionId,
}: {
  kind: NonNullable<import("@/lib/chat-types").ChatMessage["statusCard"]>;
  title?: string;
  content: string;
  actions?: ChatAction[];
  onAction?: (actionId: string) => void;
  busyActionId?: string | null;
}) {
  return (
    <div
      className={cn(
        "rounded-xl border px-4 py-3 text-sm",
        statusCardTone(kind),
      )}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-xs font-semibold uppercase tracking-wide opacity-80">
            {statusCardLabel(kind)}
          </p>
          {title && title !== statusCardLabel(kind) && (
            <p className="mt-1 font-medium">{title}</p>
          )}
          <p className="mt-1 leading-relaxed opacity-95">{content}</p>
        </div>
      </div>
      {actions && actions.length > 0 && onAction && (
        <div className="mt-3 flex flex-wrap gap-2">
          {actions.map((a) => (
            <button
              key={a.id}
              type="button"
              disabled={busyActionId === a.id}
              onClick={() => onAction(a.id)}
              className={cn(
                "rounded-lg px-3 py-1.5 text-xs font-medium transition-colors disabled:opacity-50",
                a.variant === "primary" && "bg-white/90 text-black hover:bg-white",
                a.variant === "danger" &&
                  "border border-red-400/40 bg-red-500/20 hover:bg-red-500/30",
                (!a.variant || a.variant === "secondary") &&
                  "border border-white/10 bg-black/20 hover:bg-black/30",
              )}
            >
              {busyActionId === a.id ? "…" : a.label}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
