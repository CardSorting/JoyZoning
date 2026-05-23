"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { api } from "@/lib/api";
import { runChatAction } from "@/lib/chat-actions";
import { loadRecentThreads } from "@/lib/chat-storage";
import type { ChatMessage } from "@/lib/chat-types";
import { useManagerChat } from "@/hooks/useManagerChat";
import { useSessionStatus } from "@/hooks/useSessionStatus";
import type { WatchBootstrap } from "@/lib/types";
import { ChatComposer } from "./ChatComposer";
import { ChatEmptyState } from "./ChatEmptyState";
import { ChatHeader } from "./ChatHeader";
import { ChatMessages } from "./ChatMessages";
import { ChatSidebar } from "./ChatSidebar";

export function ChatShell() {
  const router = useRouter();
  const [boot, setBoot] = useState<WatchBootstrap | null>(null);
  const [sessionId, setSessionId] = useState("");
  const [bootError, setBootError] = useState<string | null>(null);
  const [bootLoading, setBootLoading] = useState(true);
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [busyActionId, setBusyActionId] = useState<string | null>(null);
  const [recentThreads, setRecentThreads] = useState(loadRecentThreads());

  const chat = useManagerChat(sessionId || null);
  const { summary, cpHealth, refresh: refreshStatus } = useSessionStatus(
    sessionId || null,
    boot,
  );

  const refreshBoot = useCallback(async () => {
    setBootLoading(true);
    setBootError(null);
    try {
      const b = await api.bootstrap();
      setBoot(b);
      setSessionId((prev) => {
        if (prev && b.sessions.some((s) => s.id === prev)) return prev;
        return b.sessions[0]?.id ?? "";
      });
    } catch (err) {
      setBootError(err instanceof Error ? err.message : "Failed to connect");
    } finally {
      setBootLoading(false);
    }
  }, []);

  useEffect(() => {
    void refreshBoot();
  }, [refreshBoot]);

  useEffect(() => {
    setRecentThreads(loadRecentThreads());
  }, [chat.messages.length, sessionId]);

  const activeSession = boot?.sessions.find((s) => s.id === sessionId);
  const sessionName = activeSession?.name ?? activeSession?.workspaceRoot;

  const handleAction = useCallback(
    async (actionId: string, message: ChatMessage) => {
      if (!sessionId) return;
      setBusyActionId(actionId);
      try {
        await runChatAction(actionId, {
          sessionId,
          taskId: message.taskId,
          workspaceRoot: activeSession?.workspaceRoot,
          onNavigate: (path) => router.push(path),
          onNotify: (text, role) => chat.appendToolMessage(text, role ?? "tool"),
        });
        await refreshStatus();
      } catch (err) {
        chat.appendToolMessage(
          err instanceof Error ? err.message : "Action failed",
          "error",
        );
      } finally {
        setBusyActionId(null);
      }
    },
    [sessionId, activeSession?.workspaceRoot, router, chat, refreshStatus],
  );

  const handleCreateSession = useCallback(async () => {
    const workspaceRoot = window.prompt(
      "Workspace folder path (must exist on this machine):",
      activeSession?.workspaceRoot ?? "",
    );
    if (!workspaceRoot?.trim()) return;
    const name =
      window.prompt("Session name:", workspaceRoot.split("/").pop() ?? "Workspace") ??
      "Workspace";
    try {
      const created = await api.createSession(name, workspaceRoot.trim());
      await refreshBoot();
      setSessionId(created.id);
      chat.appendToolMessage(`Created session "${created.name}".`, "system");
    } catch (err) {
      chat.appendToolMessage(
        err instanceof Error ? err.message : "Could not create session",
        "error",
      );
    }
  }, [activeSession?.workspaceRoot, refreshBoot, chat]);

  const handleOpenWorkspace = useCallback(async () => {
    const path =
      activeSession?.workspaceRoot ??
      window.prompt("Workspace folder to open:");
    if (!path) return;
    try {
      await api.openPath(path);
      chat.appendToolMessage("Opened workspace folder.", "tool");
    } catch (err) {
      chat.appendToolMessage(
        err instanceof Error ? err.message : "Could not open workspace",
        "error",
      );
    }
  }, [activeSession?.workspaceRoot, chat]);

  const showEmpty = !chat.hasMessages && !chat.isStreaming;

  const header = useMemo(
    () => (
      <ChatHeader
        sessionName={sessionName}
        workspaceRoot={activeSession?.workspaceRoot}
        connectionState={chat.connectionState}
        cpHealth={cpHealth}
      />
    ),
    [
      sessionName,
      activeSession?.workspaceRoot,
      chat.connectionState,
      cpHealth,
    ],
  );

  if (bootLoading && !boot) {
    return (
      <div className="flex h-screen items-center justify-center bg-gpt-main text-gpt-muted">
        Connecting to control plane…
      </div>
    );
  }

  if (bootError && !boot) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-gpt-main px-4">
        <div className="max-w-md rounded-2xl border border-gpt-border bg-gpt-sidebar p-6 text-center">
          <h1 className="text-lg font-semibold text-gpt-text">Cannot reach JoyZoning</h1>
          <p className="mt-2 text-sm text-gpt-muted">{bootError}</p>
          <p className="mt-4 text-xs text-gpt-muted">
            Start the control plane on port 9470, then retry.
          </p>
          <button
            type="button"
            onClick={() => void refreshBoot()}
            className="mt-4 rounded-lg border border-gpt-border px-4 py-2 text-sm hover:bg-gpt-hover"
          >
            Retry connection
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="flex h-screen overflow-hidden bg-gpt-main text-gpt-text">
      <ChatSidebar
        collapsed={sidebarCollapsed}
        onToggleCollapse={() => setSidebarCollapsed((v) => !v)}
        sessions={boot?.sessions ?? []}
        sessionId={sessionId}
        onSessionChange={setSessionId}
        onNewChat={chat.clearChat}
        recentThreads={recentThreads}
        onLoadThread={chat.loadThread}
        summary={summary}
        cpHealth={cpHealth}
      />

      <div className="relative flex min-w-0 flex-1 flex-col">
        {header}

        {chat.connectionState === "reconnecting" && (
          <div className="border-b border-amber-500/30 bg-amber-500/10 px-4 py-2 text-center text-xs text-amber-100">
            Reconnecting to live updates…
          </div>
        )}

        {showEmpty ? (
          <ChatEmptyState
            hasSession={Boolean(sessionId)}
            onCreateSession={handleCreateSession}
            onOpenWorkspace={handleOpenWorkspace}
          />
        ) : (
          <ChatMessages
            messages={chat.messages}
            onAction={(id, msg) => void handleAction(id, msg)}
            onRetry={chat.retryLastSend}
            busyActionId={busyActionId}
          />
        )}

        <ChatComposer
          value={chat.input}
          onChange={chat.setInput}
          onSend={() => void chat.sendMessage()}
          onChip={(prompt) => void chat.sendText(prompt)}
          onOpenConsole={() =>
            router.push(
              sessionId
                ? `/console?sessionId=${encodeURIComponent(sessionId)}`
                : "/console",
            )
          }
          streaming={chat.isStreaming}
          sendInflight={chat.sendInflight}
          disabled={!sessionId && (boot?.sessions.length ?? 0) === 0}
          placeholder={
            sessionId
              ? "Ask Hermes to fix a bug, explain the repo, or start a bounded YOLO task…"
              : "Open or create a workspace to start chatting with Hermes…"
          }
        />
      </div>
    </div>
  );
}
