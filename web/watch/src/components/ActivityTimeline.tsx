"use client";

import {
  AlertCircle,
  CheckCircle2,
  Play,
  RefreshCw,
  Shield,
  Wrench,
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { cn } from "@/lib/cn";
import type { JoyEvent } from "@/lib/types";

function formatTime(iso?: string) {
  if (!iso) return "";
  try {
    return new Date(iso).toLocaleTimeString([], {
      hour: "2-digit",
      minute: "2-digit",
    });
  } catch {
    return iso;
  }
}

function classifyEvent(text: string): {
  icon: LucideIcon;
  tone: "ok" | "warn" | "neutral" | "active";
  short: string;
} {
  const t = text.toLowerCase();
  if (t.includes("complete") || t.includes("merged") || t.includes("done"))
    return { icon: CheckCircle2, tone: "ok", short: "Done" };
  if (t.includes("block") || t.includes("fail") || t.includes("error"))
    return { icon: AlertCircle, tone: "warn", short: "Issue" };
  if (t.includes("verif") || t.includes("check"))
    return { icon: Shield, tone: "active", short: "Verify" };
  if (t.includes("dispatch") || t.includes("start") || t.includes("lease"))
    return { icon: Play, tone: "active", short: "Start" };
  if (t.includes("retry") || t.includes("recover") || t.includes("reopen"))
    return { icon: RefreshCw, tone: "active", short: "Retry" };
  return { icon: Wrench, tone: "neutral", short: "Work" };
}

function EventChip({
  text,
  at,
  highlight,
}: {
  text: string;
  at?: string;
  highlight?: boolean;
}) {
  const { icon: Icon, tone, short } = classifyEvent(text);

  return (
    <li
      className={cn(
        "flex items-start gap-3 rounded-lg border px-3 py-2",
        highlight
          ? "border-campfire-accent/30 bg-campfire-accent/5"
          : "border-campfire-border/60 bg-campfire-elevated/40",
      )}
    >
      <span
        className={cn(
          "flex h-8 w-8 shrink-0 items-center justify-center rounded-lg",
          tone === "ok" && "bg-campfire-ok/15 text-campfire-ok",
          tone === "warn" && "bg-campfire-err/15 text-campfire-err",
          tone === "active" && "bg-campfire-accent/15 text-campfire-accent",
          tone === "neutral" && "bg-campfire-border/40 text-campfire-muted",
        )}
      >
        <Icon className="h-4 w-4" aria-hidden />
      </span>
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <span className="text-[10px] font-bold uppercase tracking-wider text-campfire-muted">
            {short}
          </span>
          {at && (
            <time className="text-[10px] text-campfire-muted">{at}</time>
          )}
        </div>
        <p className="mt-0.5 line-clamp-2 text-sm text-campfire-text">{text}</p>
      </div>
    </li>
  );
}

export function ActivityTimeline({
  recentActivity,
  events,
}: {
  recentActivity: string[];
  events: JoyEvent[];
}) {
  const items: { at?: string; text: string; highlight?: boolean }[] = [];

  for (const line of recentActivity.slice(0, 8)) {
    items.push({ text: line });
  }
  for (const ev of [...events].reverse().slice(0, 12)) {
    items.push({
      at: formatTime(ev.createdAt ?? ev.CreatedAt),
      text: ev.summary ?? ev.Summary ?? ev.type ?? ev.Type ?? "Event",
      highlight: true,
    });
  }

  if (!items.length) {
    return (
      <div className="flex flex-col items-center justify-center gap-2 py-12 text-campfire-muted">
        <Wrench className="h-8 w-8 opacity-40" />
        <p className="text-sm">Events appear as the task moves through kanban</p>
      </div>
    );
  }

  return (
    <ul className="grid gap-2 sm:grid-cols-2">
      {items.map((item, i) => (
        <EventChip key={i} text={item.text} at={item.at} highlight={item.highlight} />
      ))}
    </ul>
  );
}
