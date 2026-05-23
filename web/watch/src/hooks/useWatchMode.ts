"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import {
  isOperationalMode,
  parseModeFromUrl,
  type JoyZoningOperationalMode,
} from "@/lib/operational-modes";

const STORAGE_KEY = "jz-watch-mode";
const PINNED_KEY = "jz-watch-mode-user-pinned";

export const WATCH_MODE_STORAGE_KEY = STORAGE_KEY;

function readStoredMode(): JoyZoningOperationalMode | null {
  if (typeof window === "undefined") return null;
  const stored = sessionStorage.getItem(STORAGE_KEY);
  return isOperationalMode(stored) ? stored : null;
}

function isUserPinned(): boolean {
  if (typeof window === "undefined") return false;
  return sessionStorage.getItem(PINNED_KEY) === "1";
}

/** Persist mode to sessionStorage and ?mode= (testable helper). */
export function persistWatchMode(mode: JoyZoningOperationalMode, userPinned = true) {
  if (typeof window === "undefined") return;
  sessionStorage.setItem(STORAGE_KEY, mode);
  if (userPinned) {
    sessionStorage.setItem(PINNED_KEY, "1");
  }
  const url = new URL(window.location.href);
  url.searchParams.set("mode", mode);
  window.history.replaceState(null, "", url.toString());
}

/** Clears operator mode persistence when leaving a watch session. */
export function clearWatchSessionOnDepart() {
  if (typeof window === "undefined") return;
  sessionStorage.removeItem(STORAGE_KEY);
  sessionStorage.removeItem(PINNED_KEY);
}

function resolveInitialMode(recommendedMode?: string | null): JoyZoningOperationalMode {
  const fromUrl = parseModeFromUrl();
  if (fromUrl) return fromUrl;
  const stored = readStoredMode();
  if (stored) return stored;
  if (isOperationalMode(recommendedMode)) return recommendedMode;
  return "habitat";
}

export function useWatchMode(recommendedMode?: string | null) {
  const userPinnedRef = useRef(isUserPinned() || Boolean(parseModeFromUrl()));
  const [mode, setModeState] = useState<JoyZoningOperationalMode>(() =>
    resolveInitialMode(recommendedMode),
  );

  const setMode = useCallback((next: JoyZoningOperationalMode) => {
    userPinnedRef.current = true;
    setModeState(next);
    persistWatchMode(next, true);
  }, []);

  useEffect(() => {
    if (userPinnedRef.current) return;
    if (!recommendedMode || !isOperationalMode(recommendedMode)) return;
    const fromUrl = parseModeFromUrl();
    if (fromUrl) return;
    if (readStoredMode()) return;
    setModeState(recommendedMode);
    persistWatchMode(recommendedMode, false);
  }, [recommendedMode]);

  return { mode, setMode };
}
