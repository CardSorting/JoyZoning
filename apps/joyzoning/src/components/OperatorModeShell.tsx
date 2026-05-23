"use client";

/**
 * Canonical Watch shell — delegates to the single-screen OperatorConsole.
 * Legacy wrappers (WatchDashboard, PetShell) still mount this component.
 */

import type { WatchOperatorShellProps } from "./watch-operator-shell-props";
import { OperatorConsole } from "./OperatorConsole";

export type OperatorModeShellProps = WatchOperatorShellProps & {
  "data-legacy-wrapper"?: string;
};

export function OperatorModeShell({
  "data-legacy-wrapper": legacyWrapper,
  ...props
}: OperatorModeShellProps) {
  return (
    <div data-legacy-wrapper={legacyWrapper} data-testid="operator-mode-shell">
      <OperatorConsole {...props} />
    </div>
  );
}
