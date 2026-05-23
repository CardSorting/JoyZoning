import type { LiveTaskSnapshot } from "./types";

const LEASE_LABELS: Record<string, string> = {
  Leased: "Starting up",
  Running: "Building",
  Blocked: "Needs attention",
  Verifying: "Running checks",
  ReadyForReview: "Ready for review",
  Merged: "Merged",
  Revoked: "Stopped",
};

const ACTIVITY_LABELS: Record<string, string> = {
  active: "Active synthesis",
  idle: "Idle",
  stuck: "Stalled",
  waiting: "Waiting",
  blocked: "Blocked",
  review: "Ready for review",
  done: "Complete",
  none: "Standing by",
};

export function leaseLabel(status: string | null): string {
  if (!status) return "Waiting";
  return LEASE_LABELS[status] ?? status;
}

export function activityLabel(state: string): string {
  return ACTIVITY_LABELS[state] ?? "Working";
}

const STATUS_ICONS: Record<string, string> = {
  active: "⚡",
  idle: "💤",
  stuck: "⏳",
  waiting: "◌",
  blocked: "⚠",
  review: "✓",
  done: "★",
};

export function statusIcon(activityState: string, leaseStatus: string | null): string {
  if (leaseStatus === "ReadyForReview") return "✓";
  if (leaseStatus === "Blocked") return "⚠";
  return STATUS_ICONS[activityState] ?? "◈";
}

export function pollIntervalMs(snapshot: LiveTaskSnapshot): number {
  const sec = snapshot.recommendedPollSeconds ?? 2;
  const mode = snapshot.display?.pollMode;
  if (mode === "stopped") return 0;
  if (mode === "burst") return Math.max(800, Math.min(sec, 1) * 1000);
  if (mode === "slow") return Math.max(sec, 12) * 1000;
  return Math.max(1, Math.min(sec, 5)) * 1000;
}

export function isIdle(snapshot: LiveTaskSnapshot): boolean {
  return !snapshot.leaseStatus && Boolean(snapshot.message);
}

export function taskStatusName(code: number): string {
  const map: Record<number, string> = {
    0: "Backlog",
    1: "Planned",
    2: "In progress",
    3: "Needs approval",
    4: "Verifying",
    5: "Blocked",
    6: "Complete",
  };
  return map[code] ?? `Status ${code}`;
}
