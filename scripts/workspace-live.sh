#!/usr/bin/env bash
# Live build progress for non-technical users: step tracker, plain language, adaptive polling.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
# shellcheck source=jz-env.sh
source "$ROOT/scripts/jz-env.sh"

JOYZONING_URL="${JOYZONING_URL:-http://127.0.0.1:9470}"
TASK_ID="${JOYZONING_TASK_ID:-}"
WORKSPACE="${JOYZONING_WORKSPACE:-}"
ONCE=0
FULL=0
SIMPLE=0
SHOW_PATHS=0
CLEAR="${JOYZONING_LIVE_CLEAR:-1}"
MIN_INTERVAL="${JOYZONING_LIVE_MIN_INTERVAL:-3}"
MAX_INTERVAL="${JOYZONING_LIVE_MAX_INTERVAL:-30}"
FIXED_INTERVAL="${JOYZONING_LIVE_INTERVAL:-}"
OPEN_LIVE="${JOYZONING_LIVE_OPEN:-0}"
NOTIFY="${JOYZONING_LIVE_NOTIFY:-0}"

usage() {
  cat <<'EOF'
Usage: workspace-live.sh [options] [task-id] [workspace-root]

Friendly live progress while JoyZoning builds your project (like a package
tracker or install wizard). Updates JOYZONING_LIVE.md and .joyzoning/live.json.

Same as: jz task watch <task-id> [--workspace path]

Options:
  --once          One refresh, then exit
  --full          Always show the full dashboard (no compact pulse lines)
  --simple        Hide build steps and activity log (minimal view)
  --paths         Show folder paths on the dashboard
  --notify        macOS notification when build is ready for review
  --no-clear      Do not clear the screen when status changes
  -h, --help      Show this help

Environment:
  JOYZONING_URL                 Control plane (default http://127.0.0.1:9470)
  JOYZONING_WORKSPACE           Project folder to mirror into
  JOYZONING_LIVE_INTERVAL       Fixed seconds between updates (optional)
  JOYZONING_LIVE_OPEN=1         Open JOYZONING_LIVE.md once at start (macOS)
  JOYZONING_LIVE_CLEAR=0        Keep previous terminal output when status changes

Examples:
  ./scripts/workspace-live.sh <task-id> ~/Projects/MyApp
  ./scripts/workspace-live.sh --simple --once <task-id>
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --once) ONCE=1; shift ;;
    --full) FULL=1; shift ;;
    --simple) SIMPLE=1; shift ;;
    --paths) SHOW_PATHS=1; shift ;;
    --notify) NOTIFY=1; shift ;;
    --no-clear) CLEAR=0; shift ;;
    -h|--help) usage; exit 0 ;;
    -*)
      echo "Unknown option: $1" >&2
      usage >&2
      exit 2
      ;;
    *)
      if [[ -z "$TASK_ID" ]]; then
        TASK_ID="$1"
      elif [[ -z "$WORKSPACE" ]]; then
        WORKSPACE="$1"
      else
        echo "Unexpected argument: $1" >&2
        exit 2
      fi
      shift
      ;;
  esac
done

if [[ -z "$TASK_ID" && -f "${PWD}/.joyzoning-task-id" ]]; then
  TASK_ID="$(tr -d '[:space:]' <"${PWD}/.joyzoning-task-id")"
fi

if [[ -z "$TASK_ID" ]]; then
  usage >&2
  exit 2
fi

WORKSPACE="${WORKSPACE:-$PWD}"
FORMAT="$ROOT/scripts/workspace-live-format.py"
STATE_DIR="${JOYZONING_LIVE_STATE_DIR:-${TMPDIR:-/tmp}/joyzoning-live}"
STATE_FILE="$STATE_DIR/${TASK_ID}.json"
POLL_FILE="$STATE_DIR/${TASK_ID}.poll"
LIVE_MD="$WORKSPACE/JOYZONING_LIVE.md"
mkdir -p "$STATE_DIR"
mkdir -p "$WORKSPACE/.joyzoning"
echo "$TASK_ID" >"$WORKSPACE/.joyzoning-task-id"

cp_health() {
  curl -sf "${JOYZONING_URL}/api/health" >/dev/null 2>&1
}

mirror_fallback() {
  local wt session
  wt="$(jz --field .worktreePath task lease "$TASK_ID" 2>/dev/null || true)"
  session="$WORKSPACE"
  if [[ -z "$wt" || ! -d "$wt" ]]; then
    return 1
  fi
  rsync -a --delete \
    --exclude node_modules --exclude .git --exclude .joyzoning --exclude .claude \
    --exclude .vscode --exclude .expo --exclude dist --exclude build \
    "$wt/" "$session/"
}

write_fallback_status() {
  local session="$WORKSPACE"
  local live="$LIVE_MD"
  local status wt blocked
  status="$(jz --field .status task lease "$TASK_ID" 2>/dev/null || echo "?")"
  wt="$(jz --field .worktreePath task lease "$TASK_ID" 2>/dev/null || echo "")"
  blocked="$(jz task lease "$TASK_ID" 2>/dev/null | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('blockedReason') or '')" 2>/dev/null || true)"
  cat >"$live" <<EOF
# Build progress

**Status:** Waiting for control plane or no active lease.

- **Task ID:** \`$TASK_ID\`
- **Lease code:** $status
$( [[ -n "$blocked" ]] && echo "- **Note:** $blocked" )

Open this file again after starting the task, or run \`jz task run $TASK_ID\`.
EOF
  TASK_ID="$TASK_ID" STATUS="$status" BLOCKED="$blocked" WT="$wt" SESSION="$session" LIVE="$live" STATE_FILE="$STATE_FILE" \
    python3 <<'PY'
import json, os
from pathlib import Path
payload = {
    "taskId": os.environ["TASK_ID"],
    "leaseStatus": os.environ["STATUS"],
    "blockedReason": os.environ.get("BLOCKED") or None,
    "worktreePath": os.environ.get("WT") or "",
    "sessionWorkspaceRoot": os.environ["SESSION"],
    "liveFile": os.environ["LIVE"],
    "progress": {},
    "recentEvidence": [],
}
Path(os.environ["STATE_FILE"]).write_text(json.dumps({"payload": payload, "display": {}}))
PY
}

fetch_live() {
  local tmp
  tmp="$(mktemp)"
  if curl -sf -X POST "${JOYZONING_URL}/api/tasks/${TASK_ID}/live/refresh" -o "$tmp" 2>/dev/null \
    || curl -sf "${JOYZONING_URL}/api/tasks/${TASK_ID}/live" -o "$tmp" 2>/dev/null; then
    cat "$tmp"
    rm -f "$tmp"
    return 0
  fi
  rm -f "$tmp"
  return 1
}

render_response() {
  local resp="$1"
  local args=()
  [[ "$FULL" -eq 1 ]] && args+=(--full)
  [[ "$CLEAR" -eq 1 ]] && args+=(--clear)
  [[ "$SIMPLE" -eq 1 ]] && args+=(--simple)
  [[ "$SHOW_PATHS" -eq 1 ]] && args+=(--paths)
  JOYZONING_TASK_ID="$TASK_ID" \
  JOYZONING_URL="$JOYZONING_URL" \
  JOYZONING_POLL_FILE="$POLL_FILE" \
  JOYZONING_CP_OK="${JOYZONING_CP_OK:-1}" \
    printf '%s' "$resp" | python3 "$FORMAT" --stdin --state "$STATE_FILE" "${args[@]}"
}

clamp_interval() {
  local n="$1"
  if [[ -z "$n" || "$n" -le 0 ]]; then
    echo 15
    return
  fi
  if [[ "$n" -lt "$MIN_INTERVAL" ]]; then
    echo "$MIN_INTERVAL"
  elif [[ "$n" -gt "$MAX_INTERVAL" ]]; then
    echo "$MAX_INTERVAL"
  else
    echo "$n"
  fi
}

tick_once() {
  local resp poll status
  if cp_health; then
    JOYZONING_CP_OK=1
    if resp="$(fetch_live)"; then
      render_response "$resp"
      poll="$(cat "$POLL_FILE" 2>/dev/null || echo 10)"
      status="$(printf '%s' "$resp" | python3 -c "import sys,json; print(json.load(sys.stdin).get('leaseStatus') or '')" 2>/dev/null || true)"
      if [[ -n "$FIXED_INTERVAL" ]]; then
        echo "$FIXED_INTERVAL" >"$POLL_FILE"
      elif [[ "$status" == "ReadyForReview" || "$status" == "Merged" || "$status" == "Revoked" ]]; then
        echo 0 >"$POLL_FILE"
      else
        clamp_interval "$poll" >"$POLL_FILE"
      fi
      return 0
    fi
    JOYZONING_CP_OK=0
  else
    JOYZONING_CP_OK=0
  fi

  mirror_fallback || true
  write_fallback_status
  render_response "$(python3 -c "import json; print(json.load(open('$STATE_FILE'))['payload'])" 2>/dev/null || echo '{}')"
  if [[ -n "$FIXED_INTERVAL" ]]; then
    echo "$FIXED_INTERVAL" >"$POLL_FILE"
  else
    echo 15 >"$POLL_FILE"
  fi
}

notify_macos() {
  local title="$1"
  local msg="$2"
  [[ "$NOTIFY" != "1" ]] && return 0
  [[ "$(uname -s)" != "Darwin" ]] && return 0
  osascript -e "display notification \"$msg\" with title \"$title\"" 2>/dev/null || true
}

print_welcome() {
  cat <<EOF

  JoyZoning — live build tracker
  ─────────────────────────────
  Project folder: $WORKSPACE
  Status file:    $LIVE_MD
  Machine view:   $WORKSPACE/.joyzoning/live.json

  Step-by-step progress (like a package tracker). Between updates you'll
  see one live status line. Open JOYZONING_LIVE.md in your editor anytime.

EOF
  if ! cp_health; then
    echo "  ⚠  Control plane is not reachable at $JOYZONING_URL"
    echo "     Using basic fallback sync until it is back."
    echo ""
  fi
}

print_welcome

if [[ "$OPEN_LIVE" == "1" ]] && [[ "$(uname -s)" == "Darwin" ]]; then
  touch "$LIVE_MD"
  open "$LIVE_MD" 2>/dev/null || true
fi

while true; do
  tick_once || true
  sleep_secs="$(tr -d '[:space:]' <"$POLL_FILE" 2>/dev/null || echo 15)"
  [[ "$ONCE" -eq 1 ]] && break
  if [[ -z "$sleep_secs" || "$sleep_secs" -eq 0 ]] 2>/dev/null; then
    echo ""
    status="$(python3 -c "import json; d=json.load(open('$STATE_FILE')); print((d.get('payload') or {}).get('leaseStatus',''))" 2>/dev/null || true)"
    if [[ "$status" == "ReadyForReview" ]]; then
      notify_macos "JoyZoning" "Your project is ready for review."
      echo "Ready for review — open your project folder and try the app."
    else
      echo "Build reached a finished state — live tracker stopped."
    fi
    break
  fi
  sleep "$sleep_secs"
done
