"use client";

import { Check, Circle, CircleDot, X } from "lucide-react";
import { cn } from "@/lib/cn";
import type { JourneyStep } from "@/lib/types";

function StepIcon({ state }: { state: JourneyStep["state"] }) {
  if (state === "complete") return <Check className="h-4 w-4" strokeWidth={3} />;
  if (state === "failed") return <X className="h-4 w-4" strokeWidth={3} />;
  if (state === "current") return <CircleDot className="h-4 w-4" />;
  return <Circle className="h-4 w-4" />;
}

export function JourneyStepper({ steps }: { steps: JourneyStep[] }) {
  return (
    <ol className="relative space-y-0" aria-label="Build stages">
      {steps.map((step, i) => (
        <li key={step.id} className="relative flex gap-4 pb-8 last:pb-0">
          {i < steps.length - 1 && (
            <span
              className="absolute left-[15px] top-8 h-[calc(100%-8px)] w-0.5 bg-campfire-border"
              aria-hidden
            />
          )}
          <span
            className={cn(
              "relative z-10 flex h-8 w-8 shrink-0 items-center justify-center rounded-full border-2",
              step.state === "complete" && "border-campfire-ok bg-campfire-ok text-campfire-bg",
              step.state === "current" &&
                "border-campfire-accent bg-campfire-accent text-campfire-bg shadow-[0_0_16px_rgba(255,140,66,0.45)]",
              step.state === "failed" && "border-campfire-err bg-campfire-err text-white",
              step.state === "pending" && "border-campfire-border bg-campfire-elevated text-campfire-muted",
            )}
          >
            <StepIcon state={step.state} />
          </span>
          <div className="pt-0.5">
            <p
              className={cn(
                "font-semibold",
                step.state === "current" && "text-campfire-accent",
                step.state === "complete" && "text-campfire-text",
                step.state === "failed" && "text-campfire-err",
                step.state === "pending" && "text-campfire-muted",
              )}
            >
              {step.label}
              {step.state === "current" && (
                <span className="ml-2 text-xs font-normal text-campfire-accent">← you are here</span>
              )}
            </p>
          </div>
        </li>
      ))}
    </ol>
  );
}
