import type { ChatMessage, RecentChatThread } from "@/lib/chat-types";

const THREADS_KEY = "jz.chat.threads";
const MESSAGES_PREFIX = "jz.chat.messages.";

export function loadRecentThreads(): RecentChatThread[] {
  if (typeof window === "undefined") return [];
  try {
    const raw = localStorage.getItem(THREADS_KEY);
    return raw ? (JSON.parse(raw) as RecentChatThread[]) : [];
  } catch {
    return [];
  }
}

export function saveRecentThread(thread: RecentChatThread) {
  const existing = loadRecentThreads().filter((t) => t.id !== thread.id);
  const next = [thread, ...existing].slice(0, 12);
  localStorage.setItem(THREADS_KEY, JSON.stringify(next));
}

export function loadThreadMessages(threadId: string): ChatMessage[] {
  if (typeof window === "undefined") return [];
  try {
    const raw = localStorage.getItem(`${MESSAGES_PREFIX}${threadId}`);
    return raw ? (JSON.parse(raw) as ChatMessage[]) : [];
  } catch {
    return [];
  }
}

export function saveThreadMessages(threadId: string, messages: ChatMessage[]) {
  const trimmed = messages
    .filter((m) => !m.streaming)
    .slice(-200)
    .map(({ streaming: _s, ...rest }) => rest);
  localStorage.setItem(`${MESSAGES_PREFIX}${threadId}`, JSON.stringify(trimmed));
}

export function newThreadId() {
  return `thread-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
}
