#!/usr/bin/env bash
# CI / doctor helper — verify vendored BroccoliQ is buildable and dist exists.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DIST="$ROOT/broccoliq/dist/infrastructure/index.js"
WORKER="$ROOT/broccoliq/worker/joy-bridge.mjs"

fail() {
  echo "broccoliq-verify: $*" >&2
  exit 1
}

[[ -f "$ROOT/broccoliq/package.json" ]] || fail "broccoliq/package.json missing"

if [[ ! -f "$DIST" ]]; then
  echo "Building BroccoliQ…"
  "$ROOT/scripts/broccoliq-build.sh"
fi

[[ -f "$DIST" ]] || fail "dist not found after build"
[[ -f "$WORKER" ]] || fail "joy-bridge worker missing"

echo "broccoliq-verify: ok ($DIST)"
