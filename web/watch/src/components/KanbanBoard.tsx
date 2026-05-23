"use client";

import { motion } from "framer-motion";
import { cn } from "@/lib/cn";
import {
  KANBAN_COLUMNS,
  TASK_STATUS_LABEL,
  columnForTaskStatus,
  tasksByColumn,
  type BoardTask,
} from "@/lib/kanban";

export function KanbanBoard({
  tasks,
  activeTaskId,
  onSelectTask,
}: {
  tasks: BoardTask[];
  activeTaskId: string;
  onSelectTask?: (id: string) => void;
}) {
  const grouped = tasksByColumn(tasks);

  return (
    <section aria-label="Kanban board" className="overflow-x-auto pb-1">
      <div className="flex min-w-[640px] gap-2">
        {KANBAN_COLUMNS.map((col) => {
          const cards = grouped.get(col.id) ?? [];
          const hasActive = cards.some(
            (c) => c.id.toLowerCase() === activeTaskId.toLowerCase(),
          );

          return (
            <div
              key={col.id}
              className={cn(
                "flex min-w-[120px] flex-1 flex-col rounded-xl border p-2 transition-colors",
                hasActive
                  ? "border-campfire-accent/50 bg-campfire-accent/5"
                  : "border-campfire-border bg-campfire-elevated/60",
              )}
            >
              <header className="mb-2 flex items-center justify-between px-1">
                <span className="text-[10px] font-bold uppercase tracking-widest text-campfire-muted">
                  {col.label}
                </span>
                <span
                  className={cn(
                    "flex h-5 min-w-[20px] items-center justify-center rounded-full px-1.5 text-[10px] font-bold",
                    cards.length > 0
                      ? "bg-campfire-border text-campfire-text"
                      : "text-campfire-muted",
                  )}
                >
                  {cards.length}
                </span>
              </header>

              <ul className="flex flex-1 flex-col gap-1.5">
                {cards.length === 0 && (
                  <li className="rounded-lg border border-dashed border-campfire-border/60 px-2 py-4 text-center text-[10px] text-campfire-muted">
                    —
                  </li>
                )}
                {cards.map((task) => {
                  const isActive =
                    task.id.toLowerCase() === activeTaskId.toLowerCase();
                  const statusCol = columnForTaskStatus(task.status);

                  return (
                    <li key={task.id}>
                      <motion.button
                        type="button"
                        layout
                        onClick={() => onSelectTask?.(task.id)}
                        disabled={!onSelectTask}
                        className={cn(
                          "w-full rounded-lg border px-2 py-2 text-left transition-all",
                          isActive
                            ? "border-campfire-accent bg-campfire-accent/15 shadow-[0_0_12px_rgba(255,140,66,0.25)]"
                            : "border-campfire-border/80 bg-campfire-surface hover:border-campfire-accent/40",
                          !onSelectTask && "cursor-default",
                        )}
                      >
                        <p className="line-clamp-2 text-xs font-medium leading-tight text-campfire-text">
                          {(task.title || "Task").slice(0, 48)}
                        </p>
                        <p className="mt-1 text-[10px] text-campfire-muted">
                          {TASK_STATUS_LABEL[task.status] ?? statusCol.label}
                        </p>
                        {isActive && (
                          <span className="mt-1.5 inline-flex items-center gap-1 text-[10px] font-semibold text-campfire-accent">
                            <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-campfire-accent" />
                            Watching
                          </span>
                        )}
                      </motion.button>
                    </li>
                  );
                })}
              </ul>
            </div>
          );
        })}
      </div>
    </section>
  );
}
