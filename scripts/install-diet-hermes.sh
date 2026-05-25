#!/usr/bin/env bash
# JoyZoning — non-interactive setup for ONE diet-hermes checkout (no second Hermes install).
# Manager vs executor roles are separate Hermes sessions on the same install, synced via kanban.
set -euo pipefail

log_status() { printf 'JZ_STATUS:%s\n' "$1"; }
log_progress() { printf 'JZ_PROGRESS:%s\n' "$1"; }
log_error()  { printf 'JZ_ERROR:%s\n' "$1" >&2; }

HOME_DIR="${HOME:-$(eval echo ~)}"
DIET_HERMES_DIR="${JOYZONING_DIET_HERMES_DIR:-$HOME_DIR/Downloads/diet-hermes-main-master}"
DIET_HERMES_GIT_URL="${DIET_HERMES_GIT_URL:-https://github.com/NousResearch/hermes-agent.git}"
HERMES_PROFILE="${JOYZONING_HERMES_PROFILE:-joyzoning}"

export UV_NO_CONFIG=1
export PATH="$HOME_DIR/.local/bin:$PATH"

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    log_error "Missing required command: $1"
    exit 2
  fi
}

ensure_uv() {
  if command -v uv >/dev/null 2>&1; then
    return 0
  fi
  if [ -x "$HOME_DIR/.local/bin/uv" ]; then
    export PATH="$HOME_DIR/.local/bin:$PATH"
    return 0
  fi
  log_status "Installing uv (Python package manager)…"
  local logf
  logf="$(mktemp 2>/dev/null || echo /tmp/jz-uv-install.log)"
  if ! curl -LsSf https://astral.sh/uv/install.sh -o "$logf.sh" 2>"$logf"; then
    log_error "Could not download uv installer."
    exit 1
  fi
  sh "$logf.sh" >>"$logf" 2>&1 || {
    log_error "uv install failed — see $logf"
    exit 1
  }
  rm -f "$logf" "$logf.sh"
  export PATH="$HOME_DIR/.local/bin:$PATH"
}

ensure_checkout() {
  if [ -d "$DIET_HERMES_DIR" ] && [ -f "$DIET_HERMES_DIR/pyproject.toml" ]; then
    return 0
  fi
  log_status "Downloading diet-hermes…"
  mkdir -p "$(dirname "$DIET_HERMES_DIR")"
  git clone --depth 1 "$DIET_HERMES_GIT_URL" "$DIET_HERMES_DIR"
}

find_hermes_cli() {
  for v in .venv venv; do
    if [ -x "$DIET_HERMES_DIR/$v/bin/hermes" ]; then
      echo "$DIET_HERMES_DIR/$v/bin/hermes"
      return 0
    fi
  done
  return 1
}

setup_python_env() {
  cd "$DIET_HERMES_DIR"
  if find_hermes_cli >/dev/null 2>&1; then
    log_status "diet-hermes Python environment already ready."
    log_progress 65
    return 0
  fi

  log_status "Installing Python dependencies (first run may take several minutes)…"
  ensure_uv

  if [ -d ".venv" ]; then
    rm -rf .venv
  fi

  uv venv .venv --python 3.11
  export PATH="$DIET_HERMES_DIR/.venv/bin:$HOME_DIR/.local/bin:$PATH"

  if [ -f "uv.lock" ]; then
    if UV_PROJECT_ENVIRONMENT="$DIET_HERMES_DIR/.venv" uv sync --extra all --locked; then
      log_progress 65
      return 0
    fi
    log_status "Lockfile sync failed — falling back to pip resolve…"
  fi

  uv pip install -e ".[all]" || uv pip install -e "." || {
    log_error "Failed to install diet-hermes dependencies"
    exit 1
  }
  log_progress 65
}

upsert_env() {
  local key="$1" value="$2" file="$3"
  mkdir -p "$(dirname "$file")"
  touch "$file"
  if grep -q "^${key}=" "$file" 2>/dev/null; then
    if [[ "$(uname -s)" == "Darwin" ]]; then
      sed -i '' "s|^${key}=.*|${key}=${value}|" "$file"
    else
      sed -i "s|^${key}=.*|${key}=${value}|" "$file"
    fi
  else
    printf '\n%s=%s\n' "$key" "$value" >>"$file"
  fi
}

configure_jsdp_harness() {
  local hermes_bin="$1"
  local joyzoning_repo="${JOYZONING_REPO:-$(cd "$(dirname "$0")/.." && pwd)}"
  local jz_cli="$joyzoning_repo/scripts/joyzoning"

  if [ ! -x "$jz_cli" ] && [ ! -f "$jz_cli" ]; then
    log_status "JoyZoning CLI not found at $jz_cli — skip jsdp harness config (set later in Hermes profile)."
    return 0
  fi

  log_status "Enabling JSDP rolling-horizon harness in Hermes joyzoning profile…"
  "$hermes_bin" -p "$HERMES_PROFILE" config set joyzoning.enabled true >/dev/null 2>&1 || true
  # Harness auto-detects jz_cli — optional override only when auto-detect fails:
  "$hermes_bin" -p "$HERMES_PROFILE" config set joyzoning.jsdp.harness.jz_cli "$jz_cli" >/dev/null 2>&1 || true

  local env_file="${HERMES_HOME:-$HOME_DIR/.hermes}/profiles/$HERMES_PROFILE/.env"
  upsert_env JOYZONING_MONOREPO_ROOT "$joyzoning_repo" "$env_file"
  upsert_env JOYZONING_JSDP_HARNESS 1 "$env_file"
}

configure_joyzoning_profile() {
  local hermes_bin
  hermes_bin="$(find_hermes_cli)" || {
    log_error "hermes CLI missing after setup"
    exit 1
  }

  log_status "Configuring diet-hermes for JoyZoning (API server + joyzoning profile)…"
  local hermes_home="${HERMES_HOME:-$HOME_DIR/.hermes}"
  local env_file="$hermes_home/profiles/$HERMES_PROFILE/.env"

  mkdir -p "$(dirname "$env_file")"
  if [ ! -f "$env_file" ] && [ -f "$DIET_HERMES_DIR/.env.example" ]; then
    cp "$DIET_HERMES_DIR/.env.example" "$env_file" 2>/dev/null || true
  fi
  touch "$env_file"

  upsert_env API_SERVER_ENABLED true "$env_file"
  upsert_env API_SERVER_HOST 127.0.0.1 "$env_file"
  upsert_env API_SERVER_PORT 8642 "$env_file"
  if ! grep -q '^API_SERVER_KEY=' "$env_file" 2>/dev/null; then
    local key
    key="$(openssl rand -hex 16 2>/dev/null || python3 -c 'import secrets; print(secrets.token_hex(16))')"
    upsert_env API_SERVER_KEY "$key" "$env_file"
  fi

  "$hermes_bin" -p "$HERMES_PROFILE" config set API_SERVER_ENABLED true >/dev/null 2>&1 || true
  sync_model_from_default_profile "$hermes_bin"
  configure_jsdp_harness "$hermes_bin"
  log_status "One Hermes install — Manager and executor roles use separate sessions, synced on the kanban board."
}

sync_model_from_default_profile() {
  local hermes_bin="$1"
  local hermes_home="${HERMES_HOME:-$HOME_DIR/.hermes}"
  local default_cfg="$hermes_home/config.yaml"
  local target_cfg="$hermes_home/profiles/$HERMES_PROFILE/config.yaml"

  [ -f "$default_cfg" ] || return 0
  [ -f "$target_cfg" ] || return 0

  # When default profile has a structured provider model, copy it into joyzoning so
  # JoyZoning dispatch uses the same model you configured with `hermes setup`.
  if grep -q '^model:' "$default_cfg" && grep -q 'provider:' "$default_cfg"; then
    log_status "Aligning $HERMES_PROFILE model with default Hermes profile…"
    local model provider base_url
    model="$(HERMES_CFG="$default_cfg" python3 - <<'PY' 2>/dev/null || true
import os, yaml
from pathlib import Path
cfg = yaml.safe_load(Path(os.environ["HERMES_CFG"]).read_text()) or {}
m = cfg.get("model") or {}
if isinstance(m, dict):
    print(m.get("default") or "")
PY
)"
    provider="$(HERMES_CFG="$default_cfg" python3 - <<'PY' 2>/dev/null || true
import os, yaml
from pathlib import Path
cfg = yaml.safe_load(Path(os.environ["HERMES_CFG"]).read_text()) or {}
m = cfg.get("model") or {}
if isinstance(m, dict):
    print(m.get("provider") or "")
PY
)"
    base_url="$(HERMES_CFG="$default_cfg" python3 - <<'PY' 2>/dev/null || true
import os, yaml
from pathlib import Path
cfg = yaml.safe_load(Path(os.environ["HERMES_CFG"]).read_text()) or {}
m = cfg.get("model") or {}
if isinstance(m, dict):
    print(m.get("base_url") or "")
PY
)"
    if [ -n "$model" ]; then
      "$hermes_bin" -p "$HERMES_PROFILE" config set model.default "$model" >/dev/null 2>&1 || true
    fi
    if [ -n "$provider" ]; then
      "$hermes_bin" -p "$HERMES_PROFILE" config set model.provider "$provider" >/dev/null 2>&1 || true
    fi
    if [ -n "$base_url" ]; then
      "$hermes_bin" -p "$HERMES_PROFILE" config set model.base_url "$base_url" >/dev/null 2>&1 || true
    fi
  fi
}

# --- main ---
log_progress 2
log_status "Setting up diet-hermes (single Hermes install)…"
require_cmd git
require_cmd curl

log_progress 10
ensure_checkout

log_progress 20
setup_python_env

log_progress 72
log_status "Linking hermes command…"
mkdir -p "$HOME_DIR/.local/bin"
hermes_bin="$(find_hermes_cli)"
ln -sf "$hermes_bin" "$HOME_DIR/.local/bin/hermes" 2>/dev/null || true

log_progress 78
configure_joyzoning_profile

log_progress 82
printf 'JZ_INSTALL_ROOT:%s\n' "$DIET_HERMES_DIR"
