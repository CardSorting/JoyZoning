#!/usr/bin/env bash
# Build and install the jz CLI to ~/.local/bin/jz
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${HOME}/.local/bin"
mkdir -p "$OUT" "$ROOT/dist"

export PATH="${HOME}/.dotnet:${PATH}"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "error: .NET SDK not found. Install .NET 8 SDK from https://dot.net" >&2
  exit 2
fi

dotnet publish "$ROOT/src/JoyZoning.Cli/JoyZoning.Cli.csproj" \
  -c Release \
  -o "$ROOT/dist/jz-publish" \
  --self-contained false

cp "$ROOT/dist/jz-publish/jz" "$ROOT/dist/jz"
cp "$ROOT/dist/jz" "$OUT/jz"
chmod +x "$OUT/jz" "$ROOT/dist/jz"

echo "Installed: $OUT/jz"
echo "Run: jz --help"
echo "Optional: export JOYZONING_URL=http://127.0.0.1:9470"
