"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { motion } from "framer-motion";
import { api } from "@/lib/api";
import type { IntentKind } from "@/lib/pet";
import { loadIntent } from "@/lib/session-prefs";
import type { WatchBootstrap } from "@/lib/types";
import { useLiveTask } from "@/hooks/useLiveTask";
import { clearWatchSessionOnDepart } from "@/hooks/useWatchMode";
import { tryCreateWatchLiveBinding } from "@/lib/watch-live-binding";
import { WatchOperatorDisconnected } from "./WatchOperatorDisconnected";
import { OperatorModeShell } from "./OperatorModeShell";
import { PetEntryFlow } from "./pet/PetEntryFlow";

export function WatchApp({
  initialTaskId,
  watching,
  onWatchingChange,
  onHeaderChange,
}: {
  initialTaskId: string;
  watching: boolean;
  onWatchingChange: (v: boolean) => void;
  onHeaderChange: (s: {
    connLabel: string;
    connVariant: "live" | "error" | "idle";
  }) => void;
}) {
  const [boot, setBoot] = useState<WatchBootstrap | null>(null);
  const [sessionId, setSessionId] = useState("");
  const [taskId, setTaskId] = useState(initialTaskId);
  const [taskOptions, setTaskOptions] = useState<
    { id: string; title: string; status: number }[]
  >([]);
  const [bootError, setBootError] = useState<string | null>(null);
  const [pulseTicks, setPulseTicks] = useState(0);
  const [intent, setIntent] = useState<IntentKind>("build");
  const [resting, setResting] = useState(false);
  const prevStreamLen = useRef(0);

  const live = useLiveTask(watching ? taskId : null, watching ? sessionId : null, {
    pollPaused: resting,
  });

  useEffect(() => {
    setIntent(loadIntent());
  }, []);

  useEffect(() => {
    if (!watching || resting) {
      prevStreamLen.current = 0;
      return;
    }
    if (live.stream.length > prevStreamLen.current) {
      setPulseTicks((n) => n + (live.stream.length - prevStreamLen.current));
      prevStreamLen.current = live.stream.length;
    }
  }, [watching, resting, live.stream.length]);

  useEffect(() => {
    const variant = live.connLabel.startsWith("Live")
      ? "live"
      : live.connLabel === "Error"
        ? "error"
        : "idle";
    const line =
      resting
        ? "Pet resting"
        : variant === "live"
          ? "Pet is watching"
          : variant === "error"
            ? "Connection error"
            : "Connecting…";
    onHeaderChange({ connLabel: line, connVariant: variant });
  }, [live.connLabel, resting, onHeaderChange]);

  useEffect(() => {
    api
      .health()
      .then(() => api.bootstrap())
      .then((b) => {
        setBoot(b);
        if (initialTaskId) {
          const active = b.activeTasks.find(
            (t) => t.taskId.toLowerCase() === initialTaskId.toLowerCase(),
          );
          if (active) setSessionId(active.sessionId);
          else if (b.sessions.length === 1) setSessionId(b.sessions[0].id);
        } else if (b.activeTasks.length > 0 && !taskId) {
          setTaskId(b.activeTasks[0].taskId);
          setSessionId(b.activeTasks[0].sessionId);
        }
      })
      .catch(() => setBootError("Control plane unreachable — start JoyZoning first."));
  }, [initialTaskId, taskId]);

  const loadTasks = useCallback(async (sid: string) => {
    if (!sid) {
      setTaskOptions([]);
      return;
    }
    try {
      setTaskOptions(await api.tasks(sid));
    } catch {
      setTaskOptions([]);
    }
  }, []);

  useEffect(() => {
    void loadTasks(sessionId);
  }, [sessionId, loadTasks]);

  useEffect(() => {
    if (!watching || !sessionId || resting) return;
    const t = setInterval(() => void loadTasks(sessionId), 15000);
    return () => clearInterval(t);
  }, [watching, sessionId, resting, loadTasks]);

  const handleEnter = (_intent: IntentKind) => {
    if (!taskId) return;
    onWatchingChange(true);
    const url = new URL(window.location.href);
    url.searchParams.set("taskId", taskId);
    window.history.replaceState(null, "", url.toString());
  };

  const handleDepart = () => {
    onWatchingChange(false);
    setTaskId("");
    setResting(false);
    clearWatchSessionOnDepart();
    const url = new URL(window.location.href);
    url.searchParams.delete("taskId");
    url.searchParams.delete("mode");
    window.history.replaceState(null, "", url.toString());
  };

  if (bootError) {
    return (
      <PetEmpty
        title="Can't reach the pet shop"
        body={bootError}
        hint="Start the control plane, then refresh."
      />
    );
  }

  if (!boot) {
    return (
      <div className="flex min-h-[50vh] flex-col items-center justify-center gap-3 text-pet-muted">
        <motion.span
          className="text-3xl font-bold text-pet-mint"
          animate={{ opacity: [0.5, 1, 0.5] }}
          transition={{ repeat: Infinity, duration: 2 }}
        >
          ◈
        </motion.span>
        <p>Hatching connection…</p>
      </div>
    );
  }

  return (
    <>
      {!watching && (
        <PetEntryFlow
          sessions={boot.sessions}
          activeTasks={boot.activeTasks}
          sessionId={sessionId}
          taskId={taskId}
          taskOptions={taskOptions}
          initialIntent={intent}
          onSessionChange={(id) => {
            setSessionId(id);
            setTaskId("");
          }}
          onTaskChange={setTaskId}
          onEnter={handleEnter}
        />
      )}

      {watching &&
        live.snapshot &&
        (() => {
          const bindingResult = tryCreateWatchLiveBinding({
            sessionId,
            snapshot: live.snapshot!,
            boardTasks: taskOptions,
            events: live.events,
            stream: live.stream,
            files: live.files,
            pulseTicks,
            connLabel: live.connLabel,
            resting,
            onRestingChange: setResting,
            onNudge: () => {
              void live.forceRefresh();
              if (sessionId) void loadTasks(sessionId);
              setPulseTicks((n) => n + 1);
            },
            onDepart: handleDepart,
            onSelectTask: (id) => {
              setTaskId(id);
              const url = new URL(window.location.href);
              url.searchParams.set("taskId", id);
              window.history.replaceState(null, "", url.toString());
            },
          });

          if (!bindingResult.ok) {
            return (
              <WatchOperatorDisconnected
                theme="pet"
                reason={bindingResult.error}
                hint="Fix the connection or pick another task."
                onRetry={() => void live.forceRefresh()}
              />
            );
          }

          return <OperatorModeShell {...bindingResult.binding} />;
        })()}

      {watching && !live.snapshot && !live.error && (
        <PetEmpty
          title="Pet is waking up"
          body="Pulling the first live snapshot from orchestration."
          hint="Usually just a moment."
          pulse
        />
      )}

      {watching && live.error && (
        <PetEmpty
          title="Connection blip"
          body={live.error}
          hint="Retry or leave and pick another task."
          action={{ label: "Retry", onClick: () => void live.forceRefresh() }}
        />
      )}
    </>
  );
}

function PetEmpty({
  title,
  body,
  hint,
  pulse,
  action,
}: {
  title: string;
  body: string;
  hint?: string;
  pulse?: boolean;
  action?: { label: string; onClick: () => void };
}) {
  return (
    <div className="flex min-h-[50vh] items-center justify-center px-4">
      <div className="pet-panel max-w-md p-8 text-center">
        <p className={`text-3xl font-bold text-pet-mint ${pulse ? "animate-pet-bob" : ""}`}>
          ◈
        </p>
        <p className="mt-4 text-lg font-semibold text-pet-cream">{title}</p>
        <p className="mt-2 text-sm text-pet-muted">{body}</p>
        {hint && <p className="mt-3 text-xs text-pet-muted/80">{hint}</p>}
        {action && (
          <button
            type="button"
            onClick={action.onClick}
            className="mt-6 rounded-pet-lg bg-pet-mint/25 px-5 py-2 text-sm font-semibold text-pet-mint"
          >
            {action.label}
          </button>
        )}
      </div>
    </div>
  );
}

export { PetHeader as WatchHeader } from "./pet/PetHeader";
