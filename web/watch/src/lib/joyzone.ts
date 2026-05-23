import type { LiveTaskSnapshot } from "./types";

/** Live world weather driven by orchestration (Animal Crossing–style). */
export type WorldWeather =
  | "dawn"
  | "rain"
  | "aurora"
  | "wind"
  | "eclipse"
  | "bloom"
  | "night";

export type VillageBiome = "dawn" | "aurora" | "rain" | "eclipse" | "bloom";
export type IntentSeedKind = "build" | "refine" | "repair" | "explore" | "polish";
export type SanctuaryTab =
  | "habitat"
  | "garden"
  | "flow"
  | "harmony"
  | "review"
  | "observatory";

export type CompanionMode =
  | "wandering"
  | "tending"
  | "celebrating"
  | "resting"
  | "confused";

export const VILLAGE_BIOMES: {
  id: VillageBiome;
  label: string;
  tagline: string;
  icon: string;
}[] = [
  { id: "dawn", label: "Meadow Cottage", tagline: "Soft starts and morning tea", icon: "🏡" },
  { id: "aurora", label: "Aurora Glade", tagline: "Healthy harmony and fireflies", icon: "🌲" },
  { id: "rain", label: "Rainy Workshop", tagline: "Cozy busy synthesis days", icon: "🌧️" },
  { id: "eclipse", label: "Misty Hollow", tagline: "Quiet uncertainty, still safe", icon: "🌫️" },
  { id: "bloom", label: "Bloom Garden", tagline: "Celebration and completion", icon: "🌸" },
];

export const INTENT_SEEDS: {
  id: IntentSeedKind;
  label: string;
  seedName: string;
  promise: string;
  icon: string;
}[] = [
  {
    id: "build",
    label: "Build",
    seedName: "Build Seed",
    promise: "Plant something new — the village will nurture it.",
    icon: "🌱",
  },
  {
    id: "refine",
    label: "Refine",
    seedName: "Refactor Bloom",
    promise: "Help what exists grow clearer and kinder.",
    icon: "🍃",
  },
  {
    id: "repair",
    label: "Repair",
    seedName: "Repair Seed",
    promise: "Heal friction — companions will find the way.",
    icon: "🪴",
  },
  {
    id: "explore",
    label: "Explore",
    seedName: "Exploration Seed",
    promise: "Wander gently — discoveries take their time.",
    icon: "🦋",
  },
  {
    id: "polish",
    label: "Polish",
    seedName: "Polish Seed",
    promise: "Sweep warmth and confidence into the habitat.",
    icon: "✨",
  },
];

export const INTENT_SPROUTS = [
  "UI Bloom",
  "Memory Weave",
  "Flow Stabilizer",
  "Harmony Seed",
  "Interface Bloom",
  "Confidence Pass",
  "Polish Sweep",
  "Structure Sprout",
] as const;

export const SANCTUARY_TABS: { id: SanctuaryTab; label: string; icon: string }[] = [
  { id: "habitat", label: "Habitat", icon: "🏠" },
  { id: "garden", label: "Garden", icon: "🌱" },
  { id: "flow", label: "Flow", icon: "💧" },
  { id: "harmony", label: "Harmony", icon: "🌈" },
  { id: "review", label: "Review", icon: "🌼" },
  { id: "observatory", label: "Scope", icon: "🔭" },
];

export const HABITAT_WHISPERS = [
  "The habitat is aligning.",
  "A new structure is taking shape.",
  "The synthesis spirits are focused.",
  "Harmony is returning.",
  "Flow is stabilizing.",
  "The system is resting.",
  "A breakthrough is near.",
  "The garden is flourishing.",
  "Soft rain nurtures the work.",
  "Fireflies gather at dusk.",
] as const;

export const WORLD_WEATHER_META: Record<
  WorldWeather,
  { label: string; emoji: string; mood: string }
> = {
  dawn: { label: "Dawn", emoji: "🌅", mood: "The village is waking up." },
  rain: { label: "Gentle rain", emoji: "🌧️", mood: "Busy synthesis — stay cozy." },
  aurora: { label: "Aurora skies", emoji: "🌌", mood: "Healthy harmony in the air." },
  wind: { label: "Soft wind", emoji: "🍃", mood: "Everything is aligning." },
  eclipse: { label: "Eclipse", emoji: "🌑", mood: "A pause — still safe here." },
  bloom: { label: "Bloom", emoji: "🌸", mood: "The garden is celebrating." },
  night: { label: "Starry night", emoji: "🌙", mood: "Quiet progress while you rest." },
};

export function sproutLabelForIndex(index: number, title?: string): string {
  if (title && title.length < 26) return title;
  return INTENT_SPROUTS[index % INTENT_SPROUTS.length];
}

export function resolvePhaseWhisper(snapshot: LiveTaskSnapshot): string {
  const act = snapshot.display?.activityState ?? "none";
  const lease = snapshot.leaseStatus ?? "";
  const phase = snapshot.display?.phaseLabel ?? "";

  if (act === "blocked" || lease === "Blocked")
    return "The companions look puzzled — a small steer may help.";
  if (act === "review" || lease === "ReadyForReview")
    return "The village is ready for you to visit the harvest.";
  if (lease === "Merged" || act === "done")
    return "The garden is in full bloom. Well tended.";
  if (lease === "Verifying" || phase === "Verify")
    return "The habitat is aligning — almost there.";
  if (act === "active" || lease === "Running")
    return "Rain falls softly — synthesis is busy and cozy.";
  if (lease === "Leased" || act === "waiting")
    return "Intent is forming. The village stirs.";
  if (act === "idle" || act === "stuck")
    return "The system is resting. Progress continues gently.";
  if (phase === "Setup") return "A new seed is settling into the soil.";

  return "The habitat is alive — you're doing fine.";
}

export function worldWeatherFromOrchestration(
  activity: string,
  lease: string | null,
  momentum: number,
): WorldWeather {
  if (!lease) return "night";
  if (lease === "Blocked" || activity === "blocked") return "eclipse";
  if (lease === "Merged" || activity === "done") return "bloom";
  if (lease === "ReadyForReview" || activity === "review") return "aurora";
  if (lease === "Verifying") return "wind";
  if (activity === "active" || lease === "Running" || momentum > 65) return "rain";
  if (lease === "Leased" || activity === "waiting") return "dawn";
  if (activity === "idle" || activity === "stuck") return "night";
  return "wind";
}

export function companionModeFromVitals(
  flow: number,
  confidence: number,
  stabilizing: boolean,
  fractured: boolean,
  active: boolean,
): CompanionMode {
  if (stabilizing) return "celebrating";
  if (fractured || confidence < 35) return "confused";
  if (!active && flow < 40 && confidence > 70) return "resting";
  if (flow > 70 && active) return "tending";
  if (flow > 50) return "tending";
  return "wandering";
}

export function mergeWorldWeather(
  live: WorldWeather,
  preferredBiome: VillageBiome,
): WorldWeather {
  if (["eclipse", "bloom", "rain", "aurora"].includes(live)) return live;
  const bias: Record<VillageBiome, WorldWeather> = {
    dawn: "dawn",
    aurora: "aurora",
    rain: "rain",
    eclipse: "eclipse",
    bloom: "bloom",
  };
  if (live === "night" || live === "wind") return bias[preferredBiome] ?? "dawn";
  return live;
}

export interface RecoveryAction {
  label: string;
  hint: string;
  primary: boolean;
}

export function recoveryActions(snapshot: LiveTaskSnapshot): RecoveryAction[] {
  const act = snapshot.display?.activityState;
  const lease = snapshot.leaseStatus;

  if (act === "blocked" || lease === "Blocked") {
    return [
      { label: "Clarify intent", hint: "Tell the village what you need", primary: true },
      { label: "Try again", hint: "Invite the companions back to work", primary: false },
      { label: "Observatory", hint: "Optional gentle diagnostics", primary: false },
    ];
  }
  if (act === "review" || lease === "ReadyForReview") {
    return [
      { label: "Visit harvest", hint: "See what grew", primary: true },
      { label: "Refresh", hint: "Wave hello to the village", primary: false },
    ];
  }
  return [
    { label: "Refresh", hint: "Say hello to the habitat again", primary: true },
    { label: "Observatory", hint: "For curious villagers", primary: false },
  ];
}

export function eventToWhisper(summary: string): string {
  const t = summary.toLowerCase();
  if (t.includes("complete") || t.includes("harmon"))
    return "The garden is flourishing.";
  if (t.includes("dispatch") || t.includes("start"))
    return "A new structure is taking shape.";
  if (t.includes("verif") || t.includes("align"))
    return "The habitat is aligning.";
  if (t.includes("block") || t.includes("fail"))
    return "A little friction — nothing scary.";
  if (t.includes("recover") || t.includes("retry"))
    return "Harmony is returning.";
  if (t.includes("review")) return "A breakthrough is near.";
  return HABITAT_WHISPERS[Math.abs(hashStr(summary)) % HABITAT_WHISPERS.length];
}

function hashStr(s: string): number {
  let h = 0;
  for (let i = 0; i < s.length; i++) h = (h << 5) - h + s.charCodeAt(i);
  return h;
}

export function seedPreview(kind: IntentSeedKind): string {
  switch (kind) {
    case "build":
      return "Your seed will grow in the background. You'll see the village change — never a wall of code.";
    case "refine":
      return "Companions will tidy and clarify. You watch the garden, not a diff.";
    case "repair":
      return "If something's stuck, whispers will guide you — never a scary error dump.";
    case "explore":
      return "Take it slow. Fireflies come out when discoveries are near.";
    case "polish":
      return "Warmth and confidence rise like flowers after rain.";
  }
}

/** @deprecated use VillageBiome */
export type AmbientHabitat = VillageBiome;
/** @deprecated use IntentSeedKind */
export type IntentStrand = IntentSeedKind;
/** @deprecated use SanctuaryTab */
export type HabitatTab = SanctuaryTab;
export const AMBIENT_HABITATS = VILLAGE_BIOMES;
export const INTENT_STRANDS = INTENT_SEEDS;
export const HABITAT_TABS = SANCTUARY_TABS;
export type SwarmMode = CompanionMode;
export const swarmModeFromVitals = companionModeFromVitals;
export const laneLabelForTask = sproutLabelForIndex;
export const strandPreview = seedPreview;
export const MEMORY_PARTICLES = HABITAT_WHISPERS;
export const SYNTHESIS_LANES = INTENT_SPROUTS;
export const eventToParticle = eventToWhisper;
