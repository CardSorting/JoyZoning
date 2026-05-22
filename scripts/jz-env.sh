#!/usr/bin/env bash
# Source before using jz:  source /path/to/JoyZoning/scripts/jz-env.sh
_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=dotnet-env.sh
source "$_ROOT/scripts/dotnet-env.sh"
export JOYZONING_URL="${JOYZONING_URL:-http://127.0.0.1:9470}"
export JOYZONING_DIET_HERMES_DIR="${JOYZONING_DIET_HERMES_DIR:-$HOME/Downloads/diet-hermes-main-master}"
