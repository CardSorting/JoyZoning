#!/usr/bin/env bash
# Prefer user-installed .NET 8 (global.json pins 8.0.421).
# macOS often has only .NET 6 at /usr/local/share/dotnet while 8.x lives in ~/.dotnet.
#
# Usage:  source "$(dirname "$0")/dotnet-env.sh"
# Or from repo root:  source scripts/dotnet-env.sh

if [[ -x "${HOME}/.dotnet/dotnet" ]]; then
  export DOTNET_ROOT="${HOME}/.dotnet"
  case ":${PATH}:" in
    *":${DOTNET_ROOT}:"*) ;;
    *) export PATH="${DOTNET_ROOT}:${PATH}" ;;
  esac
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo "error: dotnet not found. Install .NET 8 SDK: https://dot.net/download" >&2
  return 1 2>/dev/null || exit 1
fi

_sdk_line="$(dotnet --list-sdks 2>/dev/null | awk '/^8\./ {print; exit}')"
if [[ -z "${_sdk_line}" ]]; then
  echo "error: .NET 8 SDK required (global.json: 8.0.421). Installed SDKs:" >&2
  dotnet --list-sdks >&2 || true
  echo "Install: curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0" >&2
  return 1 2>/dev/null || exit 1
fi

unset _sdk_line
