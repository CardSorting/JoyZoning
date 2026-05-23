"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import {
  OPERATIONAL_MODES,
  type JoyZoningOperationalMode,
  type OperationalModeMeta,
} from "@/lib/operational-modes";

export type OperationalModeRegistryEntry = {
  slug: JoyZoningOperationalMode;
  title: string;
  primaryQuestion: string;
  shortDescription: string;
  isCanonicalOperationalSurface: boolean;
  forbiddenInMode: string[];
  allowsKanbanMutation: boolean;
  allowsMergeApproveRevoke: boolean;
};

function mergeRegistry(
  remote: OperationalModeRegistryEntry[] | undefined,
): Record<JoyZoningOperationalMode, OperationalModeMeta> {
  if (!remote?.length) {
    return OPERATIONAL_MODES;
  }

  const merged = { ...OPERATIONAL_MODES };
  for (const entry of remote) {
    if (!entry.slug || !merged[entry.slug]) continue;
    merged[entry.slug] = {
      ...merged[entry.slug],
      title: entry.title || merged[entry.slug].title,
      primaryQuestion: entry.primaryQuestion || merged[entry.slug].primaryQuestion,
      shortDescription: entry.shortDescription || merged[entry.slug].shortDescription,
      isCanonicalOperationalSurface: entry.isCanonicalOperationalSurface,
      forbiddenInMode: entry.forbiddenInMode?.length
        ? entry.forbiddenInMode
        : merged[entry.slug].forbiddenInMode,
    };
  }
  return merged;
}

export function useOperationalModes() {
  const [modes, setModes] = useState(OPERATIONAL_MODES);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    void api
      .operationalModes()
      .then((body) => {
        if (cancelled) return;
        setModes(mergeRegistry(body.modes as OperationalModeRegistryEntry[]));
        setError(null);
      })
      .catch((e) => {
        if (cancelled) return;
        setError(e instanceof Error ? e.message : "Mode registry unavailable");
        setModes(OPERATIONAL_MODES);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  return { modes, loading, error };
}
