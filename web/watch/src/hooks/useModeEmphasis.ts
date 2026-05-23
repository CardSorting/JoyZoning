"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import {
  isOperationalMode,
  parseModeFromUrl,
  type JoyZoningOperationalMode,
} from "@/lib/operational-modes";

const STORAGE_KEY = "jz-watch-mode-emphasis";
const PINNED_KEY = "jz-watch-mode-emphasis-pinned";

export const MODE_EMPHASIS_STORAGE_KEY = STORAGE_KEY;

function readStored(): JoyZoningOperationalMode | null {
  if (typeof window === "undefined") return null;
  const stored = sessionStorage.getItem(STORAGE_KEY);
  return isOperationalMode(stored) && stored !== "habitat" ? stored : null;
}

function isPinned(): boolean {
  if (typeof window === "undefined") return false;
  return sessionStorage.getItem(PINNED_KEY) === "1";
}

export function persistModeEmphasis(mode: JoyZoningOperationalMode, userPinned = false) {
  if (typeof window === "undefined" || mode === "habitat") return;
  sessionStorage.setItem(STORAGE_KEY, mode);
  if (userPinned) sessionStorage.setItem(PINNED_KEY, "1");
  const url = new URL(window.location.href);
  url.searchParams.set("mode", mode);
  window.history.replaceState(null, "", url.toString());
}

export function clearModeEmphasisOnDepart() {
  if (typeof window === "undefined") return;
  sessionStorage.removeItem(STORAGE_KEY);
  sessionStorage.removeItem(PINNED_KEY);
}

function resolveInitial(recommended?: string | null): JoyZoningOperationalMode {
  const fromUrl = parseModeFromUrl();
  if (fromUrl && fromUrl !== "habitat") return fromUrl;
  const stored = readStored();
  if (stored) return stored;
  if (isOperationalMode(recommended) && recommended !== "habitat") return recommended;
  return "execution";
}

/** Scroll/emphasis filter — does not hide console sections. */
export function useModeEmphasis(recommendedMode?: string | null) {
  const pinnedRef = useRef(isPinned() || Boolean(parseModeFromUrl()));
  const [emphasis, setEmphasisState] = useState<JoyZoningOperationalMode>(() =>
    resolveInitial(recommendedMode),
  );

  const setEmphasis = useCallback((next: JoyZoningOperationalMode, userPin = true) => {
    if (next === "habitat") return;
    if (userPin) pinnedRef.current = true;
    setEmphasisState(next);
    persistModeEmphasis(next, userPin);
    try {
      document.getElementById(`operator-section-${next}`)?.scrollIntoView({
        behavior: "smooth",
        block: "start",
      });
    } catch {
      /* jsdom */
    }
  }, []);

  useEffect(() => {
    if (pinnedRef.current) return;
    if (!isOperationalMode(recommendedMode) || recommendedMode === "habitat") return;
    if (parseModeFromUrl()) return;
    if (readStored()) return;
    setEmphasisState(recommendedMode);
    persistModeEmphasis(recommendedMode, false);
    if (recommendedMode === "review") {
      requestAnimationFrame(() => {
        try {
          document.getElementById("operator-section-review")?.scrollIntoView({
            behavior: "smooth",
            block: "start",
          });
        } catch {
          /* jsdom */
        }
      });
    }
  }, [recommendedMode]);

  return { emphasis, setEmphasis, recommended: recommendedMode };
}
