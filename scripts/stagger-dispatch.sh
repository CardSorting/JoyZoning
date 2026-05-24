#!/usr/bin/env bash
# Deprecated for multi-role sessions — use bounded-dispatch.sh instead.
#
# If invoked, dispatches at most one dispatchable role via bounded session plan.
exec "$(dirname "$0")/bounded-dispatch.sh" --once "$@"
