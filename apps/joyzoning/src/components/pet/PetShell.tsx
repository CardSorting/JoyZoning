"use client";

/**
 * @deprecated Use OperatorModeShell via WatchApp with a WatchLiveBinding.
 */

import { OperatorModeShell } from "../OperatorModeShell";
import {
  WatchOperatorDisconnected,
  WatchOperatorPreview,
} from "../WatchOperatorDisconnected";
import type { WatchLiveBinding } from "@/lib/watch-live-binding";
import { isWatchLiveBinding } from "@/lib/watch-live-binding";

export type PetShellLiveProps = {
  state: "live";
  binding: WatchLiveBinding;
};

export type PetShellDisconnectedProps = {
  state: "disconnected";
  reason: string;
  title?: string;
  hint?: string;
  onRetry?: () => void;
};

export type PetShellPreviewProps = {
  state: "preview";
  message: string;
  title?: string;
  hint?: string;
};

export type PetShellProps = PetShellLiveProps | PetShellDisconnectedProps | PetShellPreviewProps;

/** @deprecated Delegates to OperatorModeShell when state is live. */
export function PetShell(props: PetShellProps) {
  if (props.state === "disconnected") {
    return (
      <WatchOperatorDisconnected
        theme="pet"
        title={props.title}
        reason={props.reason}
        hint={props.hint}
        onRetry={props.onRetry}
      />
    );
  }

  if (props.state === "preview") {
    return (
      <WatchOperatorPreview theme="pet" title={props.title} message={props.message} hint={props.hint} />
    );
  }

  if (!isWatchLiveBinding(props.binding)) {
    return (
      <WatchOperatorPreview
        theme="pet"
        message="Invalid live binding — use createWatchLiveBinding() from useLiveTask."
      />
    );
  }

  return (
    <OperatorModeShell
      data-legacy-wrapper="pet-shell"
      theme="pet"
      {...props.binding}
    />
  );
}
