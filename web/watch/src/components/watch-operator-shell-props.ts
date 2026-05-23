import type { BoardTask } from "@/lib/kanban";
import type { JoyEvent, LiveTaskSnapshot, StreamLine } from "@/lib/types";

/** Shared props for the canonical Watch operator shell. */
export type WatchOperatorShellProps = {
  snapshot: LiveTaskSnapshot;
  sessionId: string;
  boardTasks: BoardTask[];
  events: JoyEvent[];
  stream: StreamLine[];
  files: string[];
  pulseTicks: number;
  connLabel: string;
  resting: boolean;
  onRestingChange: (v: boolean) => void;
  onNudge: () => void;
  onDepart: () => void;
  onSelectTask?: (id: string) => void;
  /** Campfire dashboard: highlight newly mirrored files in Workshop. */
  newFilePaths?: Set<string>;
  theme?: "pet" | "campfire";
};
