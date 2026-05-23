#!/usr/bin/env bash
# Prefer user-installed .NET 8 (global.json requires 8.0.x).
# macOS often has only .NET 6 at /usr/local/share/dotnet while 8.x lives in ~/.dotnet.
#
# Usage:  source scripts/dotnet-env.sh
# Or:     ./scripts/dotnet test ...   (shim — no source needed)
# IDE:    .vscode/settings.json sets DOTNET_ROOT for integrated terminals

_dotnet_prefer_user() {
  if [[ -x "${HOME}/.dotnet/dotnet" ]]; then
    if "${HOME}/.dotnet/dotnet" --list-sdks 2>/dev/null | grep -qE '^8\.'; then
      echo "${HOME}/.dotnet"
      return 0
    fi
  fi
  return 1
}

_dotnet_prefer_homebrew() {
  for _root in /opt/homebrew/share/dotnet /usr/local/share/dotnet; do
    if [[ -x "${_root}/dotnet" ]] && "${_root}/dotnet" --list-sdks 2>/dev/null | grep -qE '^8\.'; then
      echo "${_root}"
      return 0
    fi
  done
  return 1
}

if _root="$(_dotnet_prefer_user 2>/dev/null)"; then
  export DOTNET_ROOT="${_root}"
elif _root="$(_dotnet_prefer_homebrew 2>/dev/null)"; then
  export DOTNET_ROOT="${_root}"
elif [[ -x "${HOME}/.dotnet/dotnet" ]]; then
  export DOTNET_ROOT="${HOME}/.dotnet"
fi

if [[ -n "${DOTNET_ROOT:-}" ]]; then
  case ":${PATH}:" in
    *":${DOTNET_ROOT}:"*) ;;
    *) export PATH="${DOTNET_ROOT}:${PATH}" ;;
  esac
fi

unset -f _dotnet_prefer_user _dotnet_prefer_homebrew 2>/dev/null || true
unset _root

if ! command -v dotnet >/dev/null 2>&1; then
  echo "error: dotnet not found. Install .NET 8 SDK: https://dot.net/download" >&2
  return 1 2>/dev/null || exit 1
fi

_sdk_line="$(dotnet --list-sdks 2>/dev/null | awk '/^8\./ {print; exit}')"
if [[ -z "${_sdk_line}" ]]; then
  echo "error: .NET 8 SDK required (global.json: 8.0.x). Installed SDKs:" >&2
  dotnet --list-sdks >&2 || true
  echo "Install: curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0" >&2
  return 1 2>/dev/null || exit 1
fi

unset _sdk_line
