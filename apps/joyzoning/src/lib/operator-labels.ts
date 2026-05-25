/** User-facing labels for habitat vs runtime authority. */

/** User-facing label for POST /lease/merge (runs real git convergence by default). */
export const ACCEPT_RESULT_LABEL = "Accept result";

/** Short label for compact buttons in merge queue rows. */
export const ACCEPT_RESULT_SHORT = "Accept";

/** Request Hermes to start a managed run — habitat does not execute tools. */
export const REQUEST_MANAGED_RUN_LABEL = "Request Hermes run";

export const REQUEST_MANAGED_RUN_SHORT = "Request run";

export const REQUEST_MANAGED_RUN_TOAST = "Managed run requested on Hermes runtime.";

/** Shown when Hermes runtime reports an active supervised run. */
export const HERMES_RUN_STARTED_LABEL = "Hermes run started (supervised)";

/** Runtime observation update from supervised Hermes run. */
export const RUNTIME_OBSERVATION_LABEL = "Runtime observation";

/** Review gate — operator action required in habitat. */
export const AWAITING_OPERATOR_REVIEW_LABEL = "Awaiting operator review";

/** Panel title — habitat observes runtime workers; it does not orchestrate execution. */
export const RUNTIME_OBSERVERS_TITLE = "Runtime observers";

export const RUNTIME_OBSERVERS_DESCRIPTION =
  "Hermes executes; JoyZoning supervises JSDP role chains and convergence review.";

export const HERMES_CONVERGENCE_NOTE =
  "Convergence state mirrored from Hermes (observe-only). Accept-merge remains operator-owned in JoyZoning.";

export const HABITAT_SUPERVISION_LOADING =
  "Pulling the first live snapshot from supervised runtime.";

export const NOT_CONNECTED_SUPERVISION =
  "Not connected to live supervision";

export const LEGACY_RUNTIME_SHIM_LABEL = "LegacyRuntimeShim (dev-only, not canonical runtime)";
