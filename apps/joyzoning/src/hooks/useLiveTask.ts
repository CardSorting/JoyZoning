"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { api } from "@/lib/api";
import { isIdle, pollIntervalMs } from "@/lib/presentation";
import type { JoyEvent, LiveTaskSnapshot, StreamLine } from "@/lib/types";
import { buildWorkspaceTaskSnapshot } from "@/lib/workspace-snapshot";
import { useSignalR } from "./useSignalR";

function newLine(kind: StreamLine["kind"], text: string): StreamLine {
  return {
    id: `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
    at: new Date().toLocaleTimeString(),
    kind,
    text,
  };
}

export function useLiveTask(
  taskId: string | null,
  sessionId: string | null,
  options?: { pollPaused?: boolean },
) {
  const pollPaused = options?.pollPaused ?? false;
  const [snapshot, setSnapshot] = useState<LiveTaskSnapshot | null>(null);
  const [events, setEvents] = useState<JoyEvent[]>([]);
  const [files, setFiles] = useState<string[]>([]);
  const [newFilePaths, setNewFilePaths] = useState<Set<string>>(new Set());
  const [stream, setStream] = useState<StreamLine[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [connLabel, setConnLabel] = useState("Connecting…");
  const eventsSince = useRef(0);
  const knownFiles = useRef(new Set<string>());
  const pollRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const prevFileCount = useRef(0);

  const pushStream = useCallback((kind: StreamLine["kind"], text: string) => {
    setStream((prev) => [...prev.slice(-199), newLine(kind, text)]);
  }, []);

  const loadFiles = useCallback(async (id: string) => {
    try {
      const data = await api.changedFiles(id);
      const paths = (data.files ?? [])
        .map((f) => (typeof f === "string" ? f : f.path ?? f.Path ?? ""))
        .filter(Boolean)
        .slice(0, 50);
      const fresh = new Set<string>();
      for (const p of paths) {
        if (!knownFiles.current.has(p)) fresh.add(p);
        knownFiles.current.add(p);
      }
      setNewFilePaths(fresh);
      setFiles(paths);
      if (fresh.size > 0) {
        setTimeout(() => setNewFilePaths(new Set()), 2500);
      }
      return paths;
    } catch {
      return [];
    }
  }, []);

  const refresh = useCallback(async () => {
    if (!taskId) return;
    setLoading(true);
    setError(null);
    try {
      const [paths, ev] = await Promise.all([
        loadFiles(taskId),
        api.events(taskId, eventsSince.current || undefined),
      ]);
      if (ev.length) {
        const maxId = Math.max(...ev.map((e) => e.id ?? e.Id ?? 0), eventsSince.current);
        eventsSince.current = maxId;
        setEvents((prev) => [...prev, ...ev].slice(-80));
      }

      let leaseStatus: string | null = null;
      let title: string | undefined;
      let worktreePath: string | null = null;
      let sessionRoot: string | null = null;

      if (sessionId) {
        try {
          const workers = await api.parallelWorkers(sessionId);
          sessionRoot = workers.sessionWorkspaceRoot;
          const worker = workers.workers.find((w) => w.taskId === taskId);
          if (worker) {
            leaseStatus = worker.leaseStatus;
            title = worker.taskTitle;
            worktreePath = worker.workspacePath ?? worker.mergeReadiness?.worktreePath ?? null;
          }
        } catch {
          /* session workers optional */
        }
      }

      const copied = Math.max(0, paths.length - prevFileCount.current);
      prevFileCount.current = paths.length;
      if (copied > 0) {
        pushStream("sync", `Workspace updated · ${copied} changed file(s)`);
      }

      const live = buildWorkspaceTaskSnapshot(taskId, {
        title,
        leaseStatus,
        worktreePath,
        sessionWorkspaceRoot: sessionRoot,
        fileCount: paths.length,
        filesCopiedThisTick: copied,
        idleMessage: leaseStatus ? undefined : "No active lease — JSDP runs in the canonical workspace.",
      });

      setSnapshot(live);
      const mode = live.display?.pollMode ?? "normal";
      setConnLabel(`Workspace · ${new Date().toLocaleTimeString()} · ${mode}`);
      return live;
    } catch (e) {
      const msg = e instanceof Error ? e.message : "Failed to load";
      setError(msg);
      setConnLabel("Error");
      return null;
    } finally {
      setLoading(false);
    }
  }, [taskId, sessionId, loadFiles, pushStream]);

  const schedulePoll = useCallback(
    (live: LiveTaskSnapshot | null) => {
      if (pollRef.current) clearTimeout(pollRef.current);
      if (!taskId || !live || isIdle(live) || pollPaused) return;
      const ms = pollIntervalMs(live);
      if (ms <= 0) return;
      pollRef.current = setTimeout(() => {
        void refresh().then((live) => {
          if (live) schedulePoll(live);
        });
      }, ms);
    },
    [taskId, refresh, pollPaused],
  );

  useEffect(() => {
    knownFiles.current.clear();
    prevFileCount.current = 0;
    setStream([]);
    setFiles([]);
    setEvents([]);
    eventsSince.current = 0;
    if (!taskId) {
      setSnapshot(null);
      return;
    }
    void refresh().then((live) => {
      if (live) schedulePoll(live);
    });
    return () => {
      if (pollRef.current) clearTimeout(pollRef.current);
    };
  }, [taskId, refresh, schedulePoll]);

  useSignalR(sessionId, taskId, {
    onWorktreeRefreshed: () => void refresh(),
    onTaskLiveUpdated: (_id, copied) => {
      if (copied > 0) pushStream("sync", `Workspace updated · ${copied} file(s)`);
      void refresh();
    },
    onTerminalOutput: (_id, text) => pushStream("tool", text),
    onCodeActivity: (_id, path, kind) => {
      pushStream("file", `[${kind}] ${path}`);
      if (taskId) void loadFiles(taskId);
    },
    onAnyUpdate: () => void refresh(),
  });

  const forceRefresh = useCallback(async () => {
    if (!taskId) return;
    const live = await refresh();
    if (live) schedulePoll(live);
  }, [taskId, refresh, schedulePoll]);

  return {
    snapshot,
    events,
    files,
    newFilePaths,
    stream,
    loading,
    error,
    connLabel,
    refresh,
    forceRefresh,
  };
}
