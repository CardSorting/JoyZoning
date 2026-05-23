import type { IntentKind } from "./pet";

const INTENT_KEY = "joyzone.intent";

export function loadIntent(): IntentKind {
  if (typeof window === "undefined") return "build";
  const v = localStorage.getItem(INTENT_KEY);
  if (
    v === "build" ||
    v === "refine" ||
    v === "repair" ||
    v === "explore" ||
    v === "polish"
  )
    return v;
  return "build";
}

export function saveIntent(intent: IntentKind) {
  localStorage.setItem(INTENT_KEY, intent);
}

/** @deprecated habitat prefs — maps biome to intent for compat */
export function savePrefs(_biome: string, seed: IntentKind) {
  saveIntent(seed);
}

/** @deprecated */
export function loadBiome(): string {
  return "aurora";
}

/** @deprecated */
export const loadSeed = loadIntent;
export const loadHabitat = loadBiome;
export const loadStrand = loadIntent;
