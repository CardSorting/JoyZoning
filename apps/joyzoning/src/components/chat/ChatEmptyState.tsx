"use client";

import { FolderOpen, Plus } from "lucide-react";

export function ChatEmptyState({
  hasSession,
  onCreateSession,
  onOpenWorkspace,
}: {
  hasSession: boolean;
  onCreateSession?: () => void;
  onOpenWorkspace?: () => void;
}) {
  if (!hasSession) {
    return (
      <div className="flex flex-1 flex-col items-center justify-center px-4 pb-40 text-center">
        <h1 className="max-w-lg text-3xl font-semibold tracking-tight text-gpt-text sm:text-4xl">
          Open or create a workspace to start chatting with Hermes
        </h1>
        <p className="mt-4 max-w-md text-sm leading-relaxed text-gpt-muted">
          Hermes is your project lead. Pick a workspace session so chat, tasks, and
          autopilot status stay tied to the right repo.
        </p>
        <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
          {onCreateSession && (
            <button
              type="button"
              onClick={onCreateSession}
              className="inline-flex items-center gap-2 rounded-xl border border-gpt-border bg-gpt-elevated px-4 py-2.5 text-sm font-medium text-gpt-text hover:bg-gpt-hover"
            >
              <Plus className="h-4 w-4" />
              Create session
            </button>
          )}
          {onOpenWorkspace && (
            <button
              type="button"
              onClick={onOpenWorkspace}
              className="inline-flex items-center gap-2 rounded-xl border border-gpt-border px-4 py-2.5 text-sm text-gpt-muted hover:bg-gpt-hover hover:text-gpt-text"
            >
              <FolderOpen className="h-4 w-4" />
              Open workspace folder
            </button>
          )}
        </div>
        <p className="mt-6 text-xs text-gpt-muted">
          Control plane must be running on port 9470.
        </p>
      </div>
    );
  }

  return (
    <div className="flex flex-1 flex-col items-center justify-center px-4 pb-40 text-center">
      <h1 className="text-3xl font-semibold tracking-tight text-gpt-text sm:text-4xl">
        What can Hermes help with?
      </h1>
      <p className="mt-3 max-w-md text-sm text-gpt-muted">
        Ask Hermes to fix a bug, explain the repo, start a bounded YOLO task, or
        summarize blocked workers — all without leaving chat.
      </p>
    </div>
  );
}
