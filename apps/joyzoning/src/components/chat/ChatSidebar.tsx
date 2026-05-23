"use client";

import Link from "next/link";
import {
  LayoutGrid,
  MessageSquarePlus,
  PanelLeftClose,
  PanelLeftOpen,
  Sparkles,
  Terminal,
} from "lucide-react";
import { cn } from "@/lib/cn";
import type { RecentChatThread, SessionStatusSummary } from "@/lib/chat-types";
import type { WatchSession } from "@/lib/types";

export function ChatSidebar({
  collapsed,
  onToggleCollapse,
  sessions,
  sessionId,
  onSessionChange,
  onNewChat,
  recentThreads,
  onLoadThread,
  summary,
  cpHealth,
}: {
  collapsed: boolean;
  onToggleCollapse: () => void;
  sessions: WatchSession[];
  sessionId: string;
  onSessionChange: (id: string) => void;
  onNewChat: () => void;
  recentThreads: RecentChatThread[];
  onLoadThread: (thread: RecentChatThread) => void;
  summary: SessionStatusSummary;
  cpHealth: "ok" | "degraded" | "offline";
}) {
  return (
    <aside
      className={cn(
        "flex h-full shrink-0 flex-col border-r border-gpt-border bg-gpt-sidebar transition-[width] duration-200",
        collapsed ? "w-[52px]" : "w-[260px]",
      )}
    >
      <div className="flex items-center gap-1 p-2">
        <button
          type="button"
          onClick={onToggleCollapse}
          aria-label={collapsed ? "Expand sidebar" : "Collapse sidebar"}
          className="flex h-9 w-9 items-center justify-center rounded-lg text-gpt-muted hover:bg-gpt-hover hover:text-gpt-text"
        >
          {collapsed ? (
            <PanelLeftOpen className="h-5 w-5" />
          ) : (
            <PanelLeftClose className="h-5 w-5" />
          )}
        </button>
        {!collapsed && (
          <div className="flex min-w-0 flex-1 items-center gap-2 px-1">
            <Sparkles className="h-4 w-4 shrink-0 text-gpt-accent" />
            <span className="truncate text-sm font-semibold text-gpt-text">JoyZoning</span>
          </div>
        )}
      </div>

      {!collapsed && (
        <>
          <div className="px-2 pb-2">
            <button
              type="button"
              onClick={onNewChat}
              className="flex w-full items-center gap-2 rounded-lg px-3 py-2.5 text-sm text-gpt-text hover:bg-gpt-hover"
            >
              <MessageSquarePlus className="h-4 w-4" />
              New chat
            </button>
          </div>

          <div className="mx-2 mb-2 rounded-lg border border-gpt-border bg-gpt-elevated/50 p-3 text-xs">
            <p className="font-medium text-gpt-text">Session status</p>
            <dl className="mt-2 space-y-1 text-gpt-muted">
              <div className="flex justify-between gap-2">
                <dt>Active tasks</dt>
                <dd className="text-gpt-text">{summary.activeTaskCount}</dd>
              </div>
              <div className="flex justify-between gap-2">
                <dt>Needs review</dt>
                <dd className="text-amber-200">{summary.needsReviewCount}</dd>
              </div>
              <div className="flex justify-between gap-2">
                <dt>Blocked</dt>
                <dd className="text-red-200">{summary.blockedCount}</dd>
              </div>
              <div className="flex justify-between gap-2">
                <dt>Autopilot</dt>
                <dd className="truncate text-gpt-text">
                  {summary.autopilotEnabled ? summary.autopilotProfile : "Off"}
                </dd>
              </div>
              <div className="flex justify-between gap-2">
                <dt>Control plane</dt>
                <dd className={cpHealth === "ok" ? "text-emerald-300" : "text-amber-300"}>
                  {cpHealth}
                </dd>
              </div>
            </dl>
          </div>

          <div className="flex-1 overflow-y-auto px-2">
            <p className="px-3 py-2 text-xs font-medium uppercase tracking-wide text-gpt-muted">
              Workspaces
            </p>
            {sessions.length === 0 ? (
              <p className="px-3 py-2 text-sm text-gpt-muted">No sessions yet</p>
            ) : (
              <ul className="space-y-0.5">
                {sessions.map((s) => (
                  <li key={s.id}>
                    <button
                      type="button"
                      onClick={() => onSessionChange(s.id)}
                      className={cn(
                        "flex w-full items-start gap-2 rounded-lg px-3 py-2 text-left text-sm transition-colors",
                        sessionId === s.id
                          ? "bg-gpt-hover text-gpt-text"
                          : "text-gpt-muted hover:bg-gpt-hover hover:text-gpt-text",
                      )}
                    >
                      <LayoutGrid className="mt-0.5 h-4 w-4 shrink-0 opacity-70" />
                      <span className="line-clamp-2">{s.name || s.workspaceRoot}</span>
                    </button>
                  </li>
                ))}
              </ul>
            )}

            {recentThreads.length > 0 && (
              <>
                <p className="px-3 py-2 text-xs font-medium uppercase tracking-wide text-gpt-muted">
                  Recent chats
                </p>
                <ul className="space-y-0.5">
                  {recentThreads
                    .filter((t) => !sessionId || t.sessionId === sessionId)
                    .slice(0, 8)
                    .map((t) => (
                      <li key={t.id}>
                        <button
                          type="button"
                          onClick={() => onLoadThread(t)}
                          className="w-full truncate rounded-lg px-3 py-2 text-left text-sm text-gpt-muted hover:bg-gpt-hover hover:text-gpt-text"
                        >
                          {t.title}
                        </button>
                      </li>
                    ))}
                </ul>
              </>
            )}
          </div>

          <div className="mt-auto border-t border-gpt-border p-2">
            <p className="px-3 py-1 text-xs font-medium uppercase tracking-wide text-gpt-muted">
              Specialist views
            </p>
            <nav className="space-y-0.5">
              <SidebarLink href="/console" icon={Terminal} label="Operator console" />
            </nav>
          </div>
        </>
      )}
    </aside>
  );
}

function SidebarLink({
  href,
  icon: Icon,
  label,
}: {
  href: string;
  icon: typeof Terminal;
  label: string;
}) {
  return (
    <Link
      href={href}
      className="flex items-center gap-2 rounded-lg px-3 py-2 text-sm text-gpt-muted hover:bg-gpt-hover hover:text-gpt-text"
    >
      <Icon className="h-4 w-4" />
      {label}
    </Link>
  );
}
