"use client";

import * as signalR from "@microsoft/signalr";
import { useEffect, useRef } from "react";
import { apiUrl } from "@/lib/config";

export interface SignalRHandlers {
  onWorktreeRefreshed?: (taskId: string) => void;
  onTaskLiveUpdated?: (taskId: string, filesCopied: number) => void;
  onTerminalOutput?: (taskId: string, text: string) => void;
  onCodeActivity?: (taskId: string, path: string, kind: string) => void;
  onAnyUpdate?: () => void;
}

export function useSignalR(
  sessionId: string | null,
  taskId: string | null,
  handlers: SignalRHandlers,
) {
  const handlersRef = useRef(handlers);
  handlersRef.current = handlers;

  useEffect(() => {
    if (!sessionId || !taskId) return;

    const hub = new signalR.HubConnectionBuilder()
      .withUrl(apiUrl("/hubs/operator"))
      .withAutomaticReconnect()
      .build();

    const match = (id: string | undefined) =>
      id && id.toLowerCase() === taskId.toLowerCase();

    hub.on("OnWorktreeRefreshed", (dto: { taskId?: string; TaskId?: string }) => {
      const id = dto.taskId ?? dto.TaskId;
      if (match(id)) handlersRef.current.onWorktreeRefreshed?.(taskId);
      handlersRef.current.onAnyUpdate?.();
    });

    hub.on(
      "OnTaskLiveUpdated",
      (dto: {
        taskId?: string;
        TaskId?: string;
        filesCopiedThisTick?: number;
        FilesCopiedThisTick?: number;
      }) => {
        const id = dto.taskId ?? dto.TaskId;
        if (!match(id)) return;
        const n = dto.filesCopiedThisTick ?? dto.FilesCopiedThisTick ?? 0;
        handlersRef.current.onTaskLiveUpdated?.(taskId, n);
        handlersRef.current.onAnyUpdate?.();
      },
    );

    hub.on(
      "OnTerminalOutput",
      (dto: { correlationId?: string; CorrelationId?: string; text?: string; Text?: string }) => {
        const id = dto.correlationId ?? dto.CorrelationId;
        if (!match(id)) return;
        const text = dto.text ?? dto.Text ?? "";
        if (text) handlersRef.current.onTerminalOutput?.(taskId, text);
      },
    );

    hub.on(
      "OnCodeActivity",
      (dto: {
        taskId?: string;
        TaskId?: string;
        path?: string;
        Path?: string;
        kind?: string;
        Kind?: string;
      }) => {
        const id = dto.taskId ?? dto.TaskId;
        if (!match(id)) return;
        const path = dto.path ?? dto.Path ?? "";
        const kind = dto.kind ?? dto.Kind ?? "tool";
        if (path) handlersRef.current.onCodeActivity?.(taskId, path, kind);
      },
    );

    hub.on("OnJoyEvent", () => handlersRef.current.onAnyUpdate?.());
    hub.on("OnTaskChanged", () => handlersRef.current.onAnyUpdate?.());
    hub.on("OnExecutionUpdated", () => handlersRef.current.onAnyUpdate?.());

    let cancelled = false;

    (async () => {
      try {
        await hub.start();
        if (cancelled) return;
        await hub.invoke("SubscribeSession", sessionId);
      } catch {
        /* hub optional */
      }
    })();

    return () => {
      cancelled = true;
      hub.stop().catch(() => {});
    };
  }, [sessionId, taskId]);
}
