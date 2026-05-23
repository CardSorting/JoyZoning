#!/usr/bin/env bash
# Dev launcher: control plane in background, then desktop app.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
# shellcheck source=dotnet-env.sh
source "$ROOT/scripts/dotnet-env.sh"
# shellcheck source=broccoliq-env.sh
source "$ROOT/scripts/broccoliq-env.sh"

if [[ -f "$ROOT/broccoliq/package.json" && ! -f "$ROOT/broccoliq/dist/infrastructure/index.js" ]]; then
  echo "Building BroccoliQ (first run)…"
  "$ROOT/scripts/broccoliq-build.sh"
fi

dotnet run --project src/JoyZoning.ControlPlane &
CP_PID=$!
trap 'kill $CP_PID 2>/dev/null || true' EXIT

sleep 2
dotnet run --project src/JoyZoning.App
