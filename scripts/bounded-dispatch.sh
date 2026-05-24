#!/usr/bin/env bash
# Bounded session dispatch — prefer role-chain-dispatch.sh for multi-role programs.
exec "$(dirname "$0")/role-chain-dispatch.sh" "$@"
