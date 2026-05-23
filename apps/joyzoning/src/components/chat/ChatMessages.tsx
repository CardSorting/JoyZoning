"use client";

import { useEffect, useRef } from "react";
import { AlertTriangle, RefreshCw, Sparkles, Wrench } from "lucide-react";
import type { ChatMessage } from "@/lib/chat-types";
import { cn } from "@/lib/cn";
import { StatusCard } from "./StatusCard";

function MessageRow({
  message,
  onAction,
  onRetry,
  busyActionId,
}: {
  message: ChatMessage;
  onAction?: (actionId: string, message: ChatMessage) => void;
  onRetry?: () => void;
  busyActionId?: string | null;
}) {
  if (message.statusCard) {
    return (
      <div className="mx-auto w-full max-w-3xl px-4 py-2">
        <StatusCard
          kind={message.statusCard}
          title={message.title ?? message.taskTitle}
          content={message.content}
          actions={message.actions}
          onAction={onAction ? (id) => onAction(id, message) : undefined}
          busyActionId={busyActionId}
        />
      </div>
    );
  }

  if (message.role === "system") {
    return (
      <div className="mx-auto w-full max-w-3xl px-4 py-2">
        <p className="rounded-lg border border-gpt-border/60 bg-gpt-elevated/50 px-4 py-2.5 text-sm text-gpt-muted">
          {message.content}
        </p>
      </div>
    );
  }

  if (message.role === "error") {
    return (
      <div className="mx-auto w-full max-w-3xl px-4 py-2">
        <div className="flex items-start gap-3 rounded-xl border border-red-500/40 bg-red-500/10 px-4 py-3 text-sm text-red-100">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          <div className="min-w-0 flex-1">
            <p>{message.content}</p>
            {message.retryable && onRetry && (
              <button
                type="button"
                onClick={onRetry}
                className="mt-2 inline-flex items-center gap-1.5 rounded-lg border border-red-400/30 px-2.5 py-1 text-xs hover:bg-red-500/20"
              >
                <RefreshCw className="h-3 w-3" />
                Retry
              </button>
            )}
          </div>
        </div>
      </div>
    );
  }

  if (message.role === "tool") {
    return (
      <div className="mx-auto w-full max-w-3xl px-4 py-2">
        <div className="flex items-start gap-2 rounded-lg border border-gpt-border bg-gpt-elevated/40 px-4 py-2.5 text-sm text-gpt-muted">
          <Wrench className="mt-0.5 h-4 w-4 shrink-0 text-gpt-accent" />
          <p>{message.content}</p>
        </div>
      </div>
    );
  }

  if (message.role === "escalation") {
    return (
      <div className="mx-auto w-full max-w-3xl px-4 py-2">
        <div className="flex items-start gap-2 rounded-xl border border-amber-500/40 bg-amber-500/10 px-4 py-3 text-sm text-amber-100">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          <div>
            {message.title && <p className="font-medium">{message.title}</p>}
            <p className={cn(message.title && "mt-1")}>{message.content}</p>
          </div>
        </div>
      </div>
    );
  }

  if (message.role === "user") {
    return (
      <div className="mx-auto w-full max-w-3xl px-4 py-3">
        <div className="flex justify-end">
          <div className="max-w-[85%] rounded-3xl bg-gpt-user px-4 py-2.5 text-[15px] leading-relaxed text-gpt-text">
            <p className="whitespace-pre-wrap">{message.content}</p>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="group mx-auto w-full max-w-3xl px-4 py-4">
      <div className="flex gap-4">
        <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-gpt-accent text-white">
          <Sparkles className="h-4 w-4" />
        </div>
        <div className="min-w-0 flex-1 pt-0.5">
          <div className="mb-1 flex items-center gap-2">
            <p className="text-sm font-medium text-gpt-text">Hermes</p>
            {message.streaming && (
              <span className="text-xs text-gpt-muted">typing…</span>
            )}
          </div>
          <div className="text-[15px] leading-relaxed text-gpt-text">
            <p className="whitespace-pre-wrap">{message.content}</p>
            {message.streaming && (
              <span className="ml-0.5 inline-block h-4 w-1.5 animate-pulse rounded-sm bg-gpt-text/70 align-middle" />
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

export function ChatMessages({
  messages,
  onAction,
  onRetry,
  busyActionId,
}: {
  messages: ChatMessage[];
  onAction?: (actionId: string, message: ChatMessage) => void;
  onRetry?: () => void;
  busyActionId?: string | null;
}) {
  const bottomRef = useRef<HTMLDivElement>(null);
  const prevLenRef = useRef(0);

  useEffect(() => {
    if (messages.length !== prevLenRef.current || messages.some((m) => m.streaming)) {
      bottomRef.current?.scrollIntoView?.({ behavior: "smooth" });
    }
    prevLenRef.current = messages.length;
  }, [messages]);

  return (
    <div className="flex-1 overflow-y-auto pb-44 pt-2">
      {messages.map((m) => (
        <MessageRow
          key={m.id}
          message={m}
          onAction={onAction}
          onRetry={onRetry}
          busyActionId={busyActionId}
        />
      ))}
      <div ref={bottomRef} />
    </div>
  );
}
