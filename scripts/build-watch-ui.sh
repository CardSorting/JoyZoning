#!/usr/bin/env bash
# Build Next.js Watch UI and deploy static export to Control Plane wwwroot.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
WEB="$ROOT/web/watch"
OUT="$ROOT/src/JoyZoning.ControlPlane/wwwroot"

cd "$WEB"

if [[ ! -d node_modules ]]; then
  echo "Installing watch UI dependencies…"
  npm install
fi

echo "Building @joyzoning/watch (Next.js static export)…"
npm run build

if [[ ! -d out ]]; then
  echo "error: expected web/watch/out after build" >&2
  exit 1
fi

echo "Publishing to $OUT"
mkdir -p "$OUT"
rsync -a --delete --exclude '.gitkeep' "$WEB/out/" "$OUT/"

echo "Done. Serve via control plane: http://127.0.0.1:9470/"
