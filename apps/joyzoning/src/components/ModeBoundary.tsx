"use client";

import type { ReactNode } from "react";
import {
  OPERATIONAL_MODES,
  type JoyZoningOperationalMode,
} from "@/lib/operational-modes";

/**
 * Visual and semantic boundary between operational modes.
 * Prevents habitat/execution/review UI from reading as one undifferentiated dashboard.
 */
export function ModeBoundary({
  mode,
  title,
  subtitle,
  children,
  className = "",
}: {
  mode: JoyZoningOperationalMode;
  title?: string;
  subtitle?: string;
  children: ReactNode;
  className?: string;
}) {
  const meta = OPERATIONAL_MODES[mode];
  const heading = title ?? meta.title;

  return (
    <section
      data-joyzoning-mode={mode}
      data-canonical-surface={meta.isCanonicalOperationalSurface ? "true" : "false"}
      aria-label={heading}
      className={className}
    >
      {(heading || subtitle) && (
        <header className="mb-2">
          {heading && (
            <h3 className="text-[11px] font-bold uppercase tracking-widest text-campfire-muted">
              {heading}
            </h3>
          )}
          {subtitle && (
            <p className="mt-0.5 text-[10px] text-campfire-muted/80">{subtitle}</p>
          )}
        </header>
      )}
      {children}
    </section>
  );
}
