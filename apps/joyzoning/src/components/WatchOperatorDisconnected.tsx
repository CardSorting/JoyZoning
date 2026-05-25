"use client";

import { NOT_CONNECTED_SUPERVISION } from "@/lib/operator-labels";

/**
 * Explicit non-live shells — never mount OperatorModeShell with stub data.
 */

export function WatchOperatorDisconnected({
  title = NOT_CONNECTED_SUPERVISION,
  reason,
  hint,
  onRetry,
  theme = "campfire",
}: {
  title?: string;
  reason: string;
  hint?: string;
  onRetry?: () => void;
  theme?: "pet" | "campfire";
}) {
  const panel =
    theme === "pet"
      ? "pet-panel max-w-md p-8 text-center"
      : "max-w-md rounded-2xl border border-campfire-border bg-campfire-surface p-8 text-center";
  const titleCls = theme === "pet" ? "text-pet-cream" : "text-campfire-text";
  const mutedCls = theme === "pet" ? "text-pet-muted" : "text-campfire-muted";
  const btnCls =
    theme === "pet"
      ? "mt-6 rounded-pet-lg bg-pet-mint/25 px-5 py-2 text-sm font-semibold text-pet-mint"
      : "mt-6 rounded-xl border border-campfire-border bg-campfire-elevated px-5 py-2 text-sm font-semibold text-campfire-text";

  return (
    <div
      className="flex min-h-[40vh] items-center justify-center px-4"
      data-testid="watch-operator-disconnected"
      data-canonical-surface="false"
      role="status"
    >
      <div className={panel}>
        <p className={`text-lg font-semibold ${titleCls}`}>{title}</p>
        <p className={`mt-2 text-sm ${mutedCls}`}>{reason}</p>
        {hint && <p className={`mt-3 text-xs ${mutedCls} opacity-80`}>{hint}</p>}
        {onRetry && (
          <button type="button" onClick={onRetry} className={btnCls}>
            Retry connection
          </button>
        )}
      </div>
    </div>
  );
}

export function WatchOperatorPreview({
  title = "Preview only",
  message,
  hint,
  theme = "campfire",
}: {
  title?: string;
  message: string;
  hint?: string;
  theme?: "pet" | "campfire";
}) {
  return (
    <WatchOperatorDisconnected
      title={title}
      reason={message}
      hint={
        hint ??
        "Pass a branded WatchLiveBinding from useLiveTask — do not mount WatchDashboard with placeholder props."
      }
      theme={theme}
    />
  );
}
