#!/usr/bin/env bash
# Deprecated: use bounded-dispatch.sh (single agent, single role per session).
exec "$(dirname "$0")/bounded-dispatch.sh" "$@"
