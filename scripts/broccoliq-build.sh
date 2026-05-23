#!/usr/bin/env bash
# Build the vendored BroccoliQ package (required before joy-bridge auto-start).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT/broccoliq"

if command -v bun >/dev/null 2>&1; then
  bun install
  bun run build
elif command -v npm >/dev/null 2>&1; then
  npm install
  npm run build
else
  echo "Install Node.js (node/npm) or Bun to build broccoliq/" >&2
  exit 1
fi

echo "BroccoliQ built: $ROOT/broccoliq/dist"
