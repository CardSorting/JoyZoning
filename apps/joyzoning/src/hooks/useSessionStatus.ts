"use client";

import { useCallback, useEffect, useState } from "react";
import { api } from "@/lib/api";
import { sessionAuthorityFromSnapshot } from "@/lib/authority";
import type { SessionStatusSummary } from "@/lib/chat-types";
import type { WatchBootstrap } from "@/lib/types";
import { workerNeedsHumanReview } from "@/lib/authority";

const EMPTY: SessionStatusSummary = {
  activeTaskCount: 0,
  blockedCount: 0,
  needsReviewCount: 0,
  autopilotProfile: "Balanced-Auto",
  autopilotEnabled: true,
};

export function useSessionStatus(sessionId: string | null, boot: WatchBootstrap | null) {
  const [summary, setSummary] = useState<SessionStatusSummary>(EMPTY);
  const [cpHealth, setCpHealth] = useState<"ok" | "degraded" | "offline">("ok");

  const refresh = useCallback(async () => {
    try {
      await api.health();
      setCpHealth("ok");
    } catch {
      setCpHealth("offline");
      return;
    }

    if (!sessionId) {
      setSummary({
        ...EMPTY,
        activeTaskCount: boot?.activeTasks.length ?? 0,
      });
      return;
    }

    try {
      const [workersSnap, mergeSnap] = await Promise.all([
        api.parallelWorkers(sessionId),
        api.mergeQueue(sessionId),
      ]);
      const auth = sessionAuthorityFromSnapshot(workersSnap);
      const allWorkers = mergeSnap.allWorkers.length
        ? mergeSnap.allWorkers
        : workersSnap.workers;

      const needsReview = allWorkers.filter((w) => workerNeedsHumanReview(w)).length;
      const blocked = mergeSnap.mergeConflicts.length + workersSnap.warnings.length;

      setSummary({
        activeTaskCount:
          boot?.activeTasks.filter((t) => t.sessionId === sessionId).length ??
          workersSnap.workers.filter((w) => w.leaseStatus === "Active").length,
        blockedCount: blocked,
        needsReviewCount: needsReview,
        autopilotProfile: auth.profileLabel,
        autopilotEnabled: auth.autopilotEnabled,
      });
      setCpHealth("ok");
    } catch {
      setCpHealth("degraded");
    }
  }, [sessionId, boot]);

  useEffect(() => {
    void refresh();
    const t = setInterval(() => void refresh(), 15000);
    return () => clearInterval(t);
  }, [refresh]);

  return { summary, cpHealth, refresh };
}
