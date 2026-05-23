"use client";

import { useCallback, useEffect, useState } from "react";
import { api } from "@/lib/api";
import type { MergeQueueSnapshot } from "@/lib/merge-queue";
import type { ParallelWorkersSnapshot } from "@/lib/parallel-workers";

export function useOperatorSessionData(sessionId: string | null) {
  const [workers, setWorkers] = useState<ParallelWorkersSnapshot | null>(null);
  const [mergeQueue, setMergeQueue] = useState<MergeQueueSnapshot | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const refresh = useCallback(async () => {
    if (!sessionId) {
      setWorkers(null);
      setMergeQueue(null);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const [w, m] = await Promise.all([
        api.parallelWorkers(sessionId),
        api.mergeQueue(sessionId),
      ]);
      setWorkers(w);
      setMergeQueue(m);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load session data");
    } finally {
      setLoading(false);
    }
  }, [sessionId]);

  useEffect(() => {
    void refresh();
    const t = setInterval(() => void refresh(), 10_000);
    return () => clearInterval(t);
  }, [refresh]);

  return { workers, mergeQueue, loading, error, refresh };
}
