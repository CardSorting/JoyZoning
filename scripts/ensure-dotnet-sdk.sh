#!/usr/bin/env bash
# Ensure .NET 8 SDK is on PATH and matches global.json (install via dotnet-install if missing).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=dotnet-env.sh
source "${ROOT}/scripts/dotnet-env.sh"

_pin="$(grep -o '"version": "[^"]*"' "${ROOT}/global.json" | head -1 | sed 's/.*"\([0-9.]*\)".*/\1/')"

echo "dotnet: $(command -v dotnet)"
echo "version: $(dotnet --version)"
echo "sdks:"
dotnet --list-sdks

if dotnet --list-sdks 2>/dev/null | grep -q '^8\.'; then
  echo "OK: .NET 8 SDK available (global.json pins ${_pin}, rollForward applies)."
  exit 0
fi

echo "Installing .NET 8 SDK to \${HOME}/.dotnet ..."
curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0 --install-dir "${HOME}/.dotnet"
source "${ROOT}/scripts/dotnet-env.sh"
dotnet --list-sdks
echo "Add to ~/.zshrc:"
echo '  export DOTNET_ROOT="$HOME/.dotnet"'
echo '  export PATH="$DOTNET_ROOT:$PATH"'
