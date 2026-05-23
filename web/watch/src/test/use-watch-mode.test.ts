import { describe, expect, it, beforeEach } from "vitest";
import {
  persistWatchMode,
  WATCH_MODE_STORAGE_KEY,
  clearWatchSessionOnDepart,
} from "@/hooks/useWatchMode";
import { parseModeFromUrl, isOperationalMode } from "@/lib/operational-modes";

describe("watch mode persistence", () => {
  beforeEach(() => {
    sessionStorage.clear();
    window.history.replaceState({}, "", "/");
  });

  it("persists mode to sessionStorage and URL", () => {
    persistWatchMode("execution");
    expect(sessionStorage.getItem(WATCH_MODE_STORAGE_KEY)).toBe("execution");
    expect(parseModeFromUrl()).toBe("execution");
  });

  it("parseModeFromUrl reads ?mode= query", () => {
    window.history.replaceState({}, "", "/?mode=review");
    expect(parseModeFromUrl()).toBe("review");
    expect(isOperationalMode(parseModeFromUrl())).toBe(true);
  });

  it("rejects invalid mode slugs", () => {
    window.history.replaceState({}, "", "/?mode=stacked-legacy");
    expect(parseModeFromUrl()).toBeNull();
  });

  it("clearWatchSessionOnDepart removes stored mode", () => {
    persistWatchMode("review");
    clearWatchSessionOnDepart();
    expect(sessionStorage.getItem(WATCH_MODE_STORAGE_KEY)).toBeNull();
  });
});
