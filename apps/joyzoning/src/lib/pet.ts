import type { JoyEvent, LiveTaskSnapshot } from "./types";
import type { CareMeters } from "./vitals";

export type PetMood =
  | "calm"
  | "focused"
  | "excited"
  | "confused"
  | "sick"
  | "tired"
  | "happy"
  | "panicking";

export type PetActionId =
  | "feed"
  | "clarify"
  | "rest"
  | "retry"
  | "review"
  | "stack"
  | "stabilize"
  | "refresh";

export interface PetAction {
  id: PetActionId;
  label: string;
  hint: string;
  primary?: boolean;
}

export interface PetState {
  mood: PetMood;
  moodLabel: string;
  moodHint: string;
  phase: string;
  progress: number;
  needsYou: boolean;
  friendlyError: string | null;
  rawError: string | null;
  primaryAction: PetAction;
  secondaryActions: PetAction[];
}

export const MOOD_META: Record<
  PetMood,
  { label: string; hint: string; emoji: string; color: string }
> = {
  calm: {
    label: "Calm",
    hint: "Idle — waiting for work",
    emoji: "😌",
    color: "text-pet-sky",
  },
  focused: {
    label: "Focused",
    hint: "Active synthesis in progress",
    emoji: "🎯",
    color: "text-pet-mint",
  },
  excited: {
    label: "Excited",
    hint: "High momentum — things are moving",
    emoji: "⚡",
    color: "text-pet-gold",
  },
  confused: {
    label: "Confused",
    hint: "Intent unclear — may need clarification",
    emoji: "❓",
    color: "text-pet-lavender",
  },
  sick: {
    label: "Sick",
    hint: "Error or exception — check the basement",
    emoji: "🤒",
    color: "text-pet-rose",
  },
  tired: {
    label: "Tired",
    hint: "Stalled or low confidence",
    emoji: "😴",
    color: "text-pet-muted",
  },
  happy: {
    label: "Happy",
    hint: "Stabilized or complete",
    emoji: "😊",
    color: "text-pet-mint",
  },
  panicking: {
    label: "Panicking",
    hint: "Repeated failures or blocked execution",
    emoji: "😱",
    color: "text-pet-rose",
  },
};

const ACTIONS: Record<PetActionId, Omit<PetAction, "primary">> = {
  feed: {
    id: "feed",
    label: "Feed Intent",
    hint: "Refresh task context and nudge the run",
  },
  clarify: {
    id: "clarify",
    label: "Clarify",
    hint: "Open observatory — see what the system needs",
  },
  rest: {
    id: "rest",
    label: "Rest",
    hint: "Pause polling — let the pet nap while you think",
  },
  retry: {
    id: "retry",
    label: "Retry",
    hint: "Force a live refresh from the control plane",
  },
  review: {
    id: "review",
    label: "Review",
    hint: "Inspect deliverables and pipeline steps",
  },
  stack: {
    id: "stack",
    label: "Open Stack Trace",
    hint: "Raw blocked reason and technical signals",
  },
  stabilize: {
    id: "stabilize",
    label: "Stabilize",
    hint: "Refresh and confirm synthesis is still healthy",
  },
  refresh: {
    id: "refresh",
    label: "Refresh",
    hint: "Pull latest live snapshot",
  },
};

function action(id: PetActionId, primary?: boolean): PetAction {
  return { ...ACTIONS[id], primary };
}

function countRecentFailures(events: JoyEvent[]): number {
  return events.slice(-12).filter((e) => {
    const t = (e.summary ?? e.Summary ?? e.type ?? e.Type ?? "").toLowerCase();
    return (
      t.includes("fail") ||
      t.includes("error") ||
      t.includes("block") ||
      t.includes("exception")
    );
  }).length;
}

export function friendlyBlockedReason(blockedReason: string | null): string | null {
  if (!blockedReason) return null;
  const r = blockedReason.toLowerCase();
  if (r.includes("401") || r.includes("api") || r.includes("key"))
    return "Your pet can't reach an external API — check credentials.";
  if (r.includes("credit") || r.includes("balance"))
    return "Energy credits look low — the shop needs a top-up.";
  if (r.includes("timeout"))
    return "The run timed out — try a clearer intent or retry.";
  if (r.includes("rate"))
    return "Too many requests at once — wait a beat, then retry.";
  if (r.includes("approval"))
    return "Synthesis paused — waiting on your approval.";
  if (r.includes("exception") || r.includes("stack"))
    return "Something threw an exception — stack trace is in the basement.";
  return "The run hit a snag — details are below if you need them.";
}

export function derivePetMood(
  snapshot: LiveTaskSnapshot,
  meters: CareMeters,
  events: JoyEvent[],
): PetMood {
  const act = snapshot.display?.activityState ?? "none";
  const lease = snapshot.leaseStatus;
  const blocked = Boolean(snapshot.blockedReason) || act === "blocked" || lease === "Blocked";
  const failures = countRecentFailures(events);

  if (blocked && failures >= 2) return "panicking";
  if (blocked && snapshot.blockedReason) return "sick";
  if (blocked) return "confused";

  if (
    act === "done" ||
    lease === "Merged" ||
    act === "review" ||
    lease === "ReadyForReview"
  )
    return "happy";

  if (act === "stuck" || meters.confidence < 42) return "tired";
  if (meters.clarity < 45 && (act === "idle" || act === "waiting")) return "confused";

  const active = act === "active" || lease === "Running";
  if (active && meters.energy > 72) return "excited";
  if (active) return "focused";

  if (!lease || act === "waiting" || act === "none") return "calm";
  return "calm";
}

export function buildPetActions(mood: PetMood, snapshot: LiveTaskSnapshot): {
  primary: PetAction;
  secondary: PetAction[];
} {
  const act = snapshot.display?.activityState;
  const reviewReady =
    act === "review" || snapshot.leaseStatus === "ReadyForReview";

  switch (mood) {
    case "panicking":
      return {
        primary: action("retry", true),
        secondary: [action("stack"), action("clarify"), action("refresh")],
      };
    case "sick":
      return {
        primary: action("stack", true),
        secondary: [action("retry"), action("clarify")],
      };
    case "confused":
      return {
        primary: action("clarify", true),
        secondary: [action("feed"), action("stack")],
      };
    case "tired":
      return {
        primary: action("rest", true),
        secondary: [action("feed"), action("retry")],
      };
    case "happy":
      return {
        primary: reviewReady ? action("review", true) : action("stabilize", true),
        secondary: [action("refresh")],
      };
    case "excited":
      return {
        primary: action("stabilize", true),
        secondary: [action("feed"), action("refresh")],
      };
    case "focused":
      return {
        primary: action("stabilize", true),
        secondary: [action("feed"), action("refresh")],
      };
    case "calm":
    default:
      return {
        primary: action("feed", true),
        secondary: [action("refresh"), action("clarify")],
      };
  }
}

export function buildPetState(
  snapshot: LiveTaskSnapshot,
  meters: CareMeters,
  events: JoyEvent[],
): PetState {
  const mood = derivePetMood(snapshot, meters, events);
  const meta = MOOD_META[mood];
  const { primary, secondary } = buildPetActions(mood, snapshot);
  const d = snapshot.display;

  return {
    mood,
    moodLabel: meta.label,
    moodHint: meta.hint,
    phase: d?.phaseLabel ?? d?.currentStepTitle ?? "Standing by",
    progress: d?.progressPercent ?? 0,
    needsYou:
      mood === "sick" ||
      mood === "panicking" ||
      mood === "confused" ||
      mood === "tired" ||
      Boolean(d?.staleWarning),
    friendlyError: friendlyBlockedReason(snapshot.blockedReason),
    rawError: snapshot.blockedReason,
    primaryAction: primary,
    secondaryActions: secondary,
  };
}

export function buildThoughtBubbles(
  events: JoyEvent[],
  recentActivity: string[],
  pulseTicks: number,
): string[] {
  const fromEvents = events
    .slice(-5)
    .map((e) => e.summary ?? e.Summary ?? e.type ?? e.Type ?? "")
    .filter(Boolean)
    .map(operationalThought);

  const fromRecent = recentActivity.slice(-4).map(operationalThought);

  const merged = [...fromEvents, ...fromRecent].filter(Boolean);
  if (merged.length === 0 && pulseTicks > 0) {
    merged.push("Listening for orchestration signals…");
  }
  if (merged.length === 0) {
    merged.push("Pet is calm. No recent thoughts yet.");
  }

  return [...new Set(merged)].slice(-5);
}

function operationalThought(line: string): string {
  const t = line.toLowerCase();
  if (t.includes("complete") || t.includes("merged"))
    return "Done! That felt good.";
  if (t.includes("dispatch") || t.includes("start"))
    return "New work claimed — focusing up.";
  if (t.includes("verif") || t.includes("check"))
    return "Running checks before we celebrate.";
  if (t.includes("block") || t.includes("fail"))
    return "Hit friction — might need you.";
  if (t.includes("file") || t.includes("copy") || t.includes("sync"))
    return "Files moved — progress is real.";
  if (t.includes("tool") || t.includes("terminal"))
    return "Tool activity detected.";
  if (line.length > 72) return `${line.slice(0, 69)}…`;
  return line;
}

export type IntentKind = "build" | "refine" | "repair" | "explore" | "polish";

export const INTENT_OPTIONS: {
  id: IntentKind;
  label: string;
  icon: string;
  blurb: string;
}[] = [
  { id: "build", label: "Build", icon: "🔨", blurb: "Grow something new" },
  { id: "refine", label: "Refine", icon: "✂️", blurb: "Tidy what exists" },
  { id: "repair", label: "Repair", icon: "🩹", blurb: "Fix friction" },
  { id: "explore", label: "Explore", icon: "🔍", blurb: "Discover gently" },
  { id: "polish", label: "Polish", icon: "✨", blurb: "Confidence pass" },
];
