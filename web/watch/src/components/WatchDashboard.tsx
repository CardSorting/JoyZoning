"use client";

/**
 * Legacy campfire entry — requires a branded WatchLiveBinding or explicit disconnected/preview state.
 * @see src/lib/watch-live-binding.ts
 */

import { OperatorModeShell } from "./OperatorModeShell";
import {
  WatchOperatorDisconnected,
  WatchOperatorPreview,
} from "./WatchOperatorDisconnected";
import type { WatchLiveBinding } from "@/lib/watch-live-binding";
import { isWatchLiveBinding } from "@/lib/watch-live-binding";

export type WatchDashboardLiveProps = {
  state: "live";
  binding: WatchLiveBinding;
  newFilePaths?: Set<string>;
};

export type WatchDashboardDisconnectedProps = {
  state: "disconnected";
  reason: string;
  title?: string;
  hint?: string;
  onRetry?: () => void;
};

export type WatchDashboardPreviewProps = {
  state: "preview";
  message: string;
  title?: string;
  hint?: string;
};

export type WatchDashboardProps =
  | WatchDashboardLiveProps
  | WatchDashboardDisconnectedProps
  | WatchDashboardPreviewProps;

/**
 * Campfire-themed operator shell. Only `state: "live"` mounts canonical operator UI.
 */
export function WatchDashboard(props: WatchDashboardProps) {
  if (props.state === "disconnected") {
    return (
      <WatchOperatorDisconnected
        theme="campfire"
        title={props.title}
        reason={props.reason}
        hint={props.hint}
        onRetry={props.onRetry}
      />
    );
  }

  if (props.state === "preview") {
    return (
      <WatchOperatorPreview
        theme="campfire"
        title={props.title}
        message={props.message}
        hint={props.hint}
      />
    );
  }

  if (!isWatchLiveBinding(props.binding)) {
    return (
      <WatchOperatorPreview
        theme="campfire"
        message="Invalid live binding — use createWatchLiveBinding() from runtime sources."
      />
    );
  }

  const { binding, newFilePaths } = props;

  return (
    <OperatorModeShell
      data-legacy-wrapper="watch-dashboard"
      theme="campfire"
      {...binding}
      newFilePaths={newFilePaths}
    />
  );
}
