"use client";

import * as signalR from "@microsoft/signalr";
import { useCallback, useEffect, useRef, useState } from "react";
import { api } from "@/lib/api";
import { RUNTIME_OBSERVATION_LABEL } from "@/lib/operator-labels";
import { cardFingerprint, cardFromJoyEvent, cardsFromWorkers } from "@/lib/chat-status-cards";
import {
  loadThreadMessages,
  newThreadId,
  saveRecentThread,
  saveThreadMessages,
} from "@/lib/chat-storage";
import type {
  ChatMessage,
  HubConnectionState,
  RecentChatThread,
  StatusCardKind,
} from "@/lib/chat-types";
import { apiUrl } from "@/lib/config";

function nextId() {
  return `${Date.now()}-${Math.random().toString(36).slice(2, 9)}`;
}

function threadTitle(messages: ChatMessage[]) {
  const firstUser = messages.find((m) => m.role === "user");
  if (!firstUser) return "New chat";
  const t = firstUser.content.trim();
  return t.length > 48 ? `${t.slice(0, 45)}…` : t;
}

export function useManagerChat(sessionId: string | null) {
  const [threadId, setThreadId] = useState(() => newThreadId());
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState("");
  const [isStreaming, setIsStreaming] = useState(false);
  const [sendInflight, setSendInflight] = useState(false);
  const [connectionState, setConnectionState] = useState<HubConnectionState>("connecting");
  const [lastFailedText, setLastFailedText] = useState<string | null>(null);

  const streamingIdRef = useRef<string | null>(null);
  const cardSeenRef = useRef<Set<string>>(new Set());
  const hubRef = useRef<signalR.HubConnection | null>(null);

  const persistMessages = useCallback(
    (next: ChatMessage[], sid: string | null, tid: string) => {
      if (!sid) return;
      saveThreadMessages(tid, next);
      const title = threadTitle(next);
      saveRecentThread({
        id: tid,
        sessionId: sid,
        title,
        updatedAt: new Date().toISOString(),
      });
    },
    [],
  );

  const appendMessage = useCallback(
    (msg: ChatMessage) => {
      setMessages((prev) => {
        const next = [...prev, msg];
        persistMessages(next, sessionId, threadId);
        return next;
      });
    },
    [persistMessages, sessionId, threadId],
  );

  const appendToolMessage = useCallback(
    (content: string, role: "tool" | "error" | "system" = "tool") => {
      appendMessage({
        id: nextId(),
        role,
        content,
        timestamp: new Date().toISOString(),
      });
    },
    [appendMessage],
  );

  const appendStatusCard = useCallback(
    (partial: Omit<ChatMessage, "id" | "role">) => {
      const fp = cardFingerprint(partial);
      if (cardSeenRef.current.has(fp)) return;
      cardSeenRef.current.add(fp);
      appendMessage({
        id: nextId(),
        role: partial.statusCard === "needs_review" ? "escalation" : "system",
        ...partial,
      });
    },
    [appendMessage],
  );

  const appendDelta = useCallback((delta: string) => {
    setConnectionState("connected");
    const streamId = streamingIdRef.current;
    if (!streamId) {
      const id = nextId();
      streamingIdRef.current = id;
      setIsStreaming(true);
      setMessages((prev) => [
        ...prev,
        { id, role: "assistant", content: delta, streaming: true, timestamp: new Date().toISOString() },
      ]);
      return;
    }

    setMessages((prev) =>
      prev.map((m) =>
        m.id === streamId ? { ...m, content: m.content + delta } : m,
      ),
    );
  }, []);

  const completeStreaming = useCallback(() => {
    const streamId = streamingIdRef.current;
    setMessages((prev) => {
      const next = streamId
        ? prev.map((m) =>
            m.id === streamId ? { ...m, streaming: false } : m,
          )
        : prev;
      persistMessages(next, sessionId, threadId);
      return next;
    });
    streamingIdRef.current = null;
    setIsStreaming(false);
    setSendInflight(false);
  }, [persistMessages, sessionId, threadId]);

  const ingestWorkerSnapshot = useCallback(
    (workers: Parameters<typeof cardsFromWorkers>[0]) => {
      for (const card of cardsFromWorkers(workers)) {
        appendStatusCard(card);
      }
    },
    [appendStatusCard],
  );

  useEffect(() => {
    cardSeenRef.current.clear();
    if (!sessionId) {
      setMessages([]);
      return;
    }
    const tid = newThreadId();
    setThreadId(tid);
    setMessages(loadThreadMessages(tid));
  }, [sessionId]);

  useEffect(() => {
    if (!sessionId) {
      setConnectionState("offline");
      return;
    }

    setConnectionState("connecting");

    const hub = new signalR.HubConnectionBuilder()
      .withUrl(apiUrl("/hubs/operator"))
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .build();

    hubRef.current = hub;

    const matchSession = (sid: string | undefined) =>
      sid?.toLowerCase() === sessionId.toLowerCase();

    hub.onreconnecting(() => setConnectionState("reconnecting"));
    hub.onreconnected(() => {
      setConnectionState("connected");
      void hub.invoke("SubscribeSession", sessionId).catch(() => {});
    });
    hub.onclose(() => setConnectionState("offline"));

    hub.on(
      "OnManagerChatDelta",
      (dto: { sessionId?: string; SessionId?: string; delta?: string; Delta?: string }) => {
        const sid = dto.sessionId ?? dto.SessionId;
        if (!matchSession(sid)) return;
        const delta = dto.delta ?? dto.Delta ?? "";
        if (delta) appendDelta(delta);
      },
    );

    hub.on(
      "OnManagerChatComplete",
      (dto: { sessionId?: string; SessionId?: string }) => {
        const sid = dto.sessionId ?? dto.SessionId;
        if (!matchSession(sid)) return;
        completeStreaming();
      },
    );

    hub.on(
      "OnJoyEvent",
      (dto: {
        correlationId?: string;
        CorrelationId?: string;
        type?: string;
        Type?: string;
        payloadJson?: string;
        PayloadJson?: string;
      }) => {
        const type = dto.type ?? dto.Type ?? "";
        let summary = type;
        try {
          const payload = dto.payloadJson ?? dto.PayloadJson;
          if (payload) {
            const parsed = JSON.parse(payload) as { summary?: string; message?: string };
            summary = parsed.summary ?? parsed.message ?? type;
          }
        } catch {
          /* ignore */
        }
        const card = cardFromJoyEvent(
          type,
          summary,
          dto.correlationId ?? dto.CorrelationId,
        );
        if (card) appendStatusCard(card);
      },
    );

    hub.on("OnTaskChanged", () => {
      void api.parallelWorkers(sessionId).then((snap) => ingestWorkerSnapshot(snap.workers)).catch(() => {});
    });

    hub.on(
      "OnTaskLiveUpdated",
      (dto: { taskId?: string; TaskId?: string; headline?: string; Headline?: string }) => {
        const taskId = dto.taskId ?? dto.TaskId;
        const headline = dto.headline ?? dto.Headline;
        if (taskId && headline) {
          appendStatusCard({
            statusCard: "hermes_run_started",
            title: RUNTIME_OBSERVATION_LABEL,
            taskId,
            content: headline,
            timestamp: new Date().toISOString(),
            actions: [{ id: "open-worker", label: "Open worker", variant: "primary" }],
          });
        }
      },
    );

    hub.on(
      "OnApprovalRequested",
      (dto: { summary?: string; Summary?: string; correlationId?: string; CorrelationId?: string }) => {
        appendStatusCard({
          statusCard: "needs_review",
          title: "Approval requested",
          taskId: dto.correlationId ?? dto.CorrelationId,
          content: dto.summary ?? dto.Summary ?? "Operator approval required.",
          timestamp: new Date().toISOString(),
          actions: [{ id: "open-console", label: "Open operator console", variant: "primary" }],
        });
      },
    );

    let cancelled = false;

    (async () => {
      try {
        await hub.start();
        if (cancelled) return;
        setConnectionState("connected");
        await hub.invoke("SubscribeSession", sessionId);
        const snap = await api.parallelWorkers(sessionId);
        ingestWorkerSnapshot(snap.workers);
      } catch {
        if (!cancelled) setConnectionState("offline");
      }
    })();

    return () => {
      cancelled = true;
      hub.stop().catch(() => {});
      hubRef.current = null;
    };
  }, [sessionId, appendDelta, completeStreaming, appendStatusCard, ingestWorkerSnapshot]);

  const sendText = useCallback(
    async (text: string, { isRetry = false }: { isRetry?: boolean } = {}) => {
      const trimmed = text.trim();
      if (!trimmed || isStreaming || sendInflight) return;

      if (!isRetry) {
        appendMessage({
          id: nextId(),
          role: "user",
          content: trimmed,
          timestamp: new Date().toISOString(),
        });
      }

      setInput("");
      setLastFailedText(null);

      if (!sessionId) {
        appendMessage({
          id: nextId(),
          role: "system",
          content: "Open or create a workspace to start chatting with Hermes.",
          timestamp: new Date().toISOString(),
        });
        return;
      }

      streamingIdRef.current = null;
      setIsStreaming(true);
      setSendInflight(true);

      try {
        await api.sendManagerMessage(sessionId, trimmed);
      } catch (err) {
        completeStreaming();
        setLastFailedText(trimmed);
        appendMessage({
          id: nextId(),
          role: "error",
          content:
            err instanceof Error
              ? err.message
              : "Failed to reach control plane. Is it running on :9470?",
          timestamp: new Date().toISOString(),
          retryable: true,
        });
      }
    },
    [
      appendMessage,
      completeStreaming,
      isStreaming,
      sendInflight,
      sessionId,
    ],
  );

  const sendMessage = useCallback(() => void sendText(input), [input, sendText]);

  const retryLastSend = useCallback(() => {
    if (lastFailedText) void sendText(lastFailedText, { isRetry: true });
  }, [lastFailedText, sendText]);

  const clearChat = useCallback(() => {
    streamingIdRef.current = null;
    setIsStreaming(false);
    setSendInflight(false);
    setLastFailedText(null);
    cardSeenRef.current.clear();
    const tid = newThreadId();
    setThreadId(tid);
    setMessages([]);
  }, []);

  const loadThread = useCallback((thread: RecentChatThread) => {
    setThreadId(thread.id);
    setMessages(loadThreadMessages(thread.id));
    cardSeenRef.current.clear();
  }, []);

  return {
    messages,
    input,
    setInput,
    isStreaming,
    sendInflight,
    connectionState,
    sendMessage,
    sendText,
    retryLastSend,
    clearChat,
    loadThread,
    hasMessages: messages.length > 0,
    appendStatusCard,
    appendToolMessage,
  };
}

export type { StatusCardKind };
