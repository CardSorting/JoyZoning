"use client";

import { Loader2, RefreshCw, Wifi, WifiOff } from "lucide-react";
import type { HubConnectionState } from "@/lib/chat-types";
import { cn } from "@/lib/cn";

export function ChatHeader({
  sessionName,
  workspaceRoot,
  connectionState,
  cpHealth,
}: {
  sessionName?: string;
  workspaceRoot?: string;
  connectionState: HubConnectionState;
  cpHealth: "ok" | "degraded" | "offline";
}) {
  const connLabel =
    connectionState === "connected"
      ? "Live"
      : connectionState === "reconnecting"
        ? "Reconnecting…"
        : connectionState === "connecting"
          ? "Connecting…"
          : "Offline";

  const connTone =
    connectionState === "connected"
      ? "text-emerald-400"
      : connectionState === "reconnecting"
        ? "text-amber-300"
        : "text-gpt-muted";

  return (
    <header className="sticky top-0 z-10 border-b border-gpt-border bg-gpt-main/95 px-4 py-3 backdrop-blur-sm">
      <div className="mx-auto flex max-w-3xl items-center justify-between gap-3">
        <div className="min-w-0">
          <h2 className="truncate text-sm font-semibold text-gpt-text">
            {sessionName ?? "No workspace session"}
          </h2>
          {workspaceRoot && (
            <p className="truncate text-xs text-gpt-muted">{workspaceRoot}</p>
          )}
        </div>
        <div className="flex shrink-0 items-center gap-3 text-xs">
          <span
            className={cn(
              "flex items-center gap-1.5",
              cpHealth === "offline" ? "text-red-300" : "text-gpt-muted",
            )}
          >
            {cpHealth === "offline" ? (
              <WifiOff className="h-3.5 w-3.5" />
            ) : (
              <Wifi className="h-3.5 w-3.5" />
            )}
            Control plane {cpHealth === "ok" ? "ok" : cpHealth}
          </span>
          <span className={cn("flex items-center gap-1.5", connTone)}>
            {connectionState === "reconnecting" ? (
              <Loader2 className="h-3.5 w-3.5 animate-spin" />
            ) : connectionState === "connected" ? (
              <span className="h-2 w-2 rounded-full bg-emerald-400" />
            ) : connectionState === "connecting" ? (
              <Loader2 className="h-3.5 w-3.5 animate-spin" />
            ) : (
              <RefreshCw className="h-3.5 w-3.5" />
            )}
            {connLabel}
          </span>
        </div>
      </div>
    </header>
  );
}
