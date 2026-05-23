"use client";

import { useEffect, useRef } from "react";
import { FileCode, Terminal } from "lucide-react";
import { cn } from "@/lib/cn";
import type { StreamLine } from "@/lib/types";

export function Workshop({
  stream,
  files,
  newFilePaths,
}: {
  stream: StreamLine[];
  files: string[];
  newFilePaths: Set<string>;
}) {
  const streamRef = useRef<HTMLPreElement>(null);

  useEffect(() => {
    const el = streamRef.current;
    if (el) el.scrollTop = el.scrollHeight;
  }, [stream]);

  return (
    <div className="grid gap-4 lg:grid-cols-2">
      <div className="flex min-h-[280px] flex-col rounded-xl border border-campfire-border bg-campfire-bg/80">
        <div className="flex items-center gap-2 border-b border-campfire-border px-4 py-2 text-xs font-semibold uppercase tracking-wider text-campfire-muted">
          <Terminal className="h-3.5 w-3.5" />
          Code & tools
        </div>
        <pre
          ref={streamRef}
          className="flex-1 overflow-auto p-4 font-mono text-[11px] leading-relaxed text-campfire-text/90"
          aria-live="polite"
        >
          {stream.length === 0 ? (
            <span className="text-campfire-muted">
              Commands and file writes appear here in real time…
            </span>
          ) : (
            stream.map((line) => (
              <div key={line.id} className="mb-1.5">
                <span className="text-campfire-muted">[{line.at}] </span>
                <span
                  className={cn(
                    line.kind === "file" && "font-semibold text-campfire-accent",
                    line.kind === "sync" && "text-campfire-ok",
                  )}
                >
                  {line.text}
                </span>
              </div>
            ))
          )}
        </pre>
      </div>

      <div className="flex min-h-[280px] flex-col rounded-xl border border-campfire-border bg-campfire-bg/80">
        <div className="flex items-center gap-2 border-b border-campfire-border px-4 py-2 text-xs font-semibold uppercase tracking-wider text-campfire-muted">
          <FileCode className="h-3.5 w-3.5" />
          Files touched
        </div>
        <ul className="flex-1 overflow-auto p-2 font-mono text-xs">
          {files.length === 0 ? (
            <li className="px-2 py-4 text-campfire-muted">
              Files show up here as the AI edits your project…
            </li>
          ) : (
            files.map((path) => (
              <li
                key={path}
                className={cn(
                  "rounded-md px-2 py-1.5 transition-colors",
                  newFilePaths.has(path) && "bg-campfire-ok/10 text-campfire-ok",
                )}
              >
                {newFilePaths.has(path) && (
                  <span className="mr-2 inline-block h-1.5 w-1.5 rounded-full bg-campfire-ok" />
                )}
                {path}
              </li>
            ))
          )}
        </ul>
      </div>
    </div>
  );
}
