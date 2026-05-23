#!/usr/bin/env bash
# Source from other scripts: repo paths + default bridge URL for BroccoliQ.
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export JOYZONING_REPO_ROOT="${JOYZONING_REPO_ROOT:-$ROOT}"
export BROCCOLIQ_BRIDGE_URL="${BROCCOLIQ_BRIDGE_URL:-http://127.0.0.1:9471}"

if [[ -z "${BROCCOLIQ_DB_PATH:-}" && -n "${JOYZONING_DB_PATH:-}" ]]; then
  _dir="$(dirname "$JOYZONING_DB_PATH")"
  export BROCCOLIQ_DB_PATH="$_dir/broccoliq.db"
fi
