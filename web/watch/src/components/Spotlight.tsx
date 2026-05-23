"use client";

import { motion } from "framer-motion";
import { FileCode, Terminal } from "lucide-react";
import { cn } from "@/lib/cn";

function shortPath(text: string): string {
  const m = text.match(/(?:wrote|edit|patch|→)\s+(.+)$/i);
  if (m) return m[1].split("/").slice(-2).join("/");
  if (text.length > 56) return `${text.slice(0, 53)}…`;
  return text;
}

export function Spotlight({
  text,
  isActive,
  kind,
}: {
  text: string;
  isActive: boolean;
  kind?: "file" | "tool" | "idle";
}) {
  const isFile = kind === "file" || /file|wrote|\.tsx?|\.cs/i.test(text);
  const Icon = isFile ? FileCode : Terminal;
  const display = shortPath(text);

  return (
    <article className="rounded-2xl border border-campfire-accent/25 bg-gradient-to-br from-campfire-accent/10 to-transparent p-4">
      <div className="flex items-center gap-3">
        <span
          className={cn(
            "flex h-10 w-10 shrink-0 items-center justify-center rounded-xl",
            isActive ? "bg-campfire-accent/25 text-campfire-accent" : "bg-campfire-elevated text-campfire-muted",
          )}
        >
          <Icon className="h-5 w-5" />
        </span>
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <h3 className="text-[10px] font-bold uppercase tracking-widest text-campfire-muted">
              Live action
            </h3>
            {isActive && (
              <span className="rounded bg-campfire-ok/20 px-1.5 py-0.5 text-[9px] font-bold uppercase tracking-widest text-campfire-ok animate-pulse-soft">
                Now
              </span>
            )}
          </div>
          <p
            className={cn(
              "mt-0.5 truncate font-mono text-sm font-medium",
              isActive ? "text-campfire-text" : "text-campfire-muted",
            )}
            title={text}
          >
            {display}
          </p>
        </div>
        {isActive && (
          <motion.span
            className="flex gap-0.5"
            aria-hidden
            animate={{ opacity: [0.3, 1, 0.3] }}
            transition={{ repeat: Infinity, duration: 1.2 }}
          >
            {[0, 1, 2].map((i) => (
              <span
                key={i}
                className="h-1.5 w-1.5 rounded-full bg-campfire-accent"
              />
            ))}
          </motion.span>
        )}
      </div>
    </article>
  );
}
