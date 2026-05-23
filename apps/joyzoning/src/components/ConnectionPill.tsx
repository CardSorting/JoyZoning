"use client";

import { cn } from "@/lib/cn";

export function ConnectionPill({
  label,
  variant = "idle",
}: {
  label: string;
  variant?: "live" | "error" | "idle";
}) {
  return (
    <div
      className={cn(
        "inline-flex items-center gap-2 rounded-full border px-3 py-1.5 text-xs font-medium",
        variant === "live" && "border-campfire-ok/40 bg-campfire-ok/10 text-campfire-ok",
        variant === "error" && "border-campfire-err/40 bg-campfire-err/10 text-campfire-err",
        variant === "idle" && "border-campfire-border bg-campfire-elevated text-campfire-muted",
      )}
      role="status"
      aria-live="polite"
    >
      <span
        className={cn(
          "h-2 w-2 rounded-full",
          variant === "live" && "bg-campfire-ok animate-pulse-soft",
          variant === "error" && "bg-campfire-err",
          variant === "idle" && "bg-campfire-muted",
        )}
      />
      {label}
    </div>
  );
}
