"use client";

import { ArrowUp, Loader2, Terminal } from "lucide-react";
import { COMMAND_CHIPS } from "@/lib/chat-types";
import { cn } from "@/lib/cn";

export function ChatComposer({
  value,
  onChange,
  onSend,
  onChip,
  onOpenConsole,
  disabled,
  streaming,
  sendInflight,
  placeholder,
}: {
  value: string;
  onChange: (v: string) => void;
  onSend: () => void;
  onChip: (prompt: string) => void;
  onOpenConsole?: () => void;
  disabled?: boolean;
  streaming?: boolean;
  sendInflight?: boolean;
  placeholder?: string;
}) {
  const busy = streaming || sendInflight;
  const canSend = value.trim().length > 0 && !busy && !disabled;

  return (
    <div className="pointer-events-none absolute inset-x-0 bottom-0 bg-gradient-to-t from-gpt-main via-gpt-main/95 to-transparent pb-6 pt-8">
      <div className="pointer-events-auto mx-auto w-full max-w-3xl px-4">
        <div className="mb-3 flex flex-wrap gap-2">
          {COMMAND_CHIPS.map((chip) => (
            <button
              key={chip.id}
              type="button"
              disabled={disabled || busy}
              onClick={() => onChip(chip.prompt)}
              className="rounded-full border border-gpt-border bg-gpt-elevated/80 px-3 py-1 text-xs text-gpt-muted hover:bg-gpt-hover hover:text-gpt-text disabled:opacity-40"
            >
              {chip.label}
            </button>
          ))}
          {onOpenConsole && (
            <button
              type="button"
              onClick={onOpenConsole}
              className="inline-flex items-center gap-1 rounded-full border border-gpt-border px-3 py-1 text-xs text-gpt-muted hover:bg-gpt-hover hover:text-gpt-text"
            >
              <Terminal className="h-3 w-3" />
              Open operator console
            </button>
          )}
        </div>

        <div
          className={cn(
            "relative flex items-end rounded-3xl border border-gpt-border bg-gpt-composer shadow-gpt-composer",
            "focus-within:border-gpt-border-focus",
          )}
        >
          <textarea
            value={value}
            onChange={(e) => onChange(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter" && !e.shiftKey) {
                e.preventDefault();
                if (canSend) onSend();
              }
            }}
            rows={1}
            disabled={disabled || busy}
            placeholder={
              placeholder ??
              "Ask Hermes to fix a bug, explain the repo, or start a bounded YOLO task…"
            }
            className="max-h-52 min-h-[52px] flex-1 resize-none bg-transparent px-4 py-3.5 text-[15px] leading-relaxed text-gpt-text placeholder:text-gpt-muted focus:outline-none disabled:opacity-60"
            style={{ fieldSizing: "content" } as React.CSSProperties}
          />
          <button
            type="button"
            onClick={onSend}
            disabled={!canSend}
            aria-label="Send message"
            className={cn(
              "mb-2 mr-2 flex h-8 w-8 shrink-0 items-center justify-center rounded-full transition-colors",
              canSend
                ? "bg-white text-black hover:bg-white/90"
                : "bg-gpt-muted/30 text-gpt-muted cursor-not-allowed",
            )}
          >
            {busy ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <ArrowUp className="h-4 w-4" strokeWidth={2.5} />
            )}
          </button>
        </div>
        <p className="mt-2 text-center text-xs text-gpt-muted">
          Chat is the steering wheel. Operator views are the dashboard.
        </p>
      </div>
    </div>
  );
}
