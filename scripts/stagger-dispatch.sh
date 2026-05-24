#!/usr/bin/env bash
# Deprecated — use role-chain-dispatch.sh --next for one role at a time.
exec "$(dirname "$0")/role-chain-dispatch.sh" --once "$@"
