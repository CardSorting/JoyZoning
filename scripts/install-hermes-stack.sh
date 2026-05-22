#!/usr/bin/env bash
# Deprecated alias — JoyZoning uses a single diet-hermes install.
exec "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/install-diet-hermes.sh" "$@"
