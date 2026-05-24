#!/usr/bin/env bash
# Deprecated — use role-chain-dispatch.sh for JSDP sequential delivery.
exec "$(dirname "$0")/role-chain-dispatch.sh" "$@"
