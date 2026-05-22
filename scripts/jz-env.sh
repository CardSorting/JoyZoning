#!/usr/bin/env bash
# Source before using jz:  source /path/to/JoyZoning/scripts/jz-env.sh
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="${DOTNET_ROOT}:${PATH}"
export JOYZONING_URL="${JOYZONING_URL:-http://127.0.0.1:9470}"
export JOYZONING_DIET_HERMES_DIR="${JOYZONING_DIET_HERMES_DIR:-$HOME/Downloads/diet-hermes-main-master}"
