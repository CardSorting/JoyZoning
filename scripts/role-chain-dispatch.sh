#!/usr/bin/env bash
# JSDP sequential role delivery chain (docs/jsdp.md)
# One role · one bounded session · one task · one lease · one merge gate · next role
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
JZ="$ROOT/scripts/joyzoning"
BASE="${JOYZONING_URL:-http://127.0.0.1:9470}"

CHAIN=""
WORKSPACE=""
PROGRAM=""
CREATE=0
ONCE=0
STATUS=0
NEXT=0
CLEANUP_STALE=0
POLL_SEC=30
DELAY_SEC=90

usage() {
  cat <<EOF
JSDP role-chain dispatch — operate like a line dance, not a jazz band.

Usage:
  role-chain-dispatch.sh --chain <guid> [--status]
  role-chain-dispatch.sh --chain <guid> [--next] [--once]
  role-chain-dispatch.sh --chain <guid> --cleanup-stale
  role-chain-dispatch.sh --create --workspace <path> --program <name> [--next|--once]

Options:
  --chain <guid>         Delivery chain id
  --create               Create default 8-role JSDP chain
  --workspace <path>     Required with --create
  --program <name>       Required with --create
  --status               Print JSDP queue status and exit
  --next                 Dispatch next role only if convergence gate is open
  --once                 Alias for --next (single pass)
  --cleanup-stale        Revoke active leases on completed/blocked steps (safe preview + revoke)
  --delay SEC            Post-dispatch wait (default: 90)
  --poll SEC             Poll interval when waiting (default: 30)
EOF
  exit 2
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --chain) CHAIN="$2"; shift 2 ;;
    --create) CREATE=1; shift ;;
    --workspace) WORKSPACE="$2"; shift 2 ;;
    --program) PROGRAM="$2"; shift 2 ;;
    --once) ONCE=1; NEXT=1; shift ;;
    --next) NEXT=1; shift ;;
    --status) STATUS=1; shift ;;
    --cleanup-stale) CLEANUP_STALE=1; shift ;;
    --delay) DELAY_SEC="$2"; shift 2 ;;
    --poll) POLL_SEC="$2"; shift 2 ;;
    -h|--help) usage ;;
    *) echo "Unknown option: $1" >&2; usage ;;
  esac
done

fetch_queue() {
  curl -sf "$BASE/api/delivery-chains/$CHAIN/queue"
}

print_status() {
  local json="$1"
  python3 - <<'PY' "$json"
import json, sys
q = json.loads(sys.argv[1])
jsdp = q.get("jsdp") or {}
print(f"JSDP chain: {q.get('chainId')}")
print(f"  workspace:     {q.get('workspaceRoot')}")
print(f"  protocol:      {q.get('protocol')}")
print(f"  progress:      {q.get('completedRoles')}/{q.get('totalRoles')} roles complete")
print(f"  merge gate:    {jsdp.get('mergeGateStatus')}")
print(f"  next eligible: {jsdp.get('nextDispatchEligibility')}")
print(f"  human action:  {jsdp.get('nextHumanAction') or '-'}")
if q.get("blockReason"):
    print(f"  blocked:       {q.get('blockReason')}")
print("")
for s in q.get("steps") or []:
    flag = ">>>" if s.get("dispatchable") else "   "
    lease = s.get("leaseStatus") or "-"
    print(f"{flag} seq {s.get('sequence')}: {s.get('taskTitle')}")
    print(f"       session={s.get('sessionId')} task={s.get('taskId')}")
    print(f"       status={s.get('taskStatus')} lease={lease} gate={s.get('mergeGateStatus')}")
    if s.get("humanActionRequired"):
        print(f"       action: {s.get('humanActionRequired')}")
    if s.get("blockReason"):
        print(f"       block:  {s.get('blockReason')}")
PY
}

read_next() {
  local json="$1"
  python3 - <<'PY' "$json"
import json, sys
q = json.loads(sys.argv[1])
jsdp = q.get("jsdp") or {}
print(q.get("nextSessionId") or "")
print(q.get("nextTaskId") or "")
print(jsdp.get("nextDispatchEligibility") or "")
print(jsdp.get("nextHumanAction") or q.get("blockReason") or "")
PY
}

cleanup_stale() {
  local json="$1"
  python3 - "$json" "$JZ" <<'PY'
import json, sys, subprocess
q = json.loads(sys.argv[1])
jz = sys.argv[2]
revoked = 0
for s in q.get("steps") or []:
    if not s.get("activeLease"):
        continue
    tid = s.get("taskId")
    if not tid:
        continue
    if s.get("complete") or s.get("taskStatus") == "Blocked":
        print(f"JZ role-chain: revoking stale lease on task {tid} ({s.get('taskTitle')})")
        subprocess.run([jz, "task", "revoke", tid, "--reason", "JSDP stale lease cleanup"], check=False)
        revoked += 1
print(f"JZ JSDP: revoked {revoked} stale lease(s)")
PY
}

if [[ "$CREATE" == "1" ]]; then
  [[ -n "$WORKSPACE" && -n "$PROGRAM" ]] || usage
  echo "JZ JSDP: creating 8-role chain for '$PROGRAM' @ $WORKSPACE"
  RESP="$(curl -sf -X POST "$BASE/api/delivery-chains" \
    -H 'Content-Type: application/json' \
    -d "$(PROGRAM="$PROGRAM" WORKSPACE="$WORKSPACE" python3 -c 'import json, os; print(json.dumps({"programName": os.environ["PROGRAM"], "workspaceRoot": os.environ["WORKSPACE"]}))')")"
  CHAIN="$(python3 -c "import json,sys; print(json.load(sys.stdin)['chainId'])" <<<"$RESP")"
  echo "JZ JSDP: chainId=$CHAIN protocol=$(python3 -c "import json,sys; print(json.load(sys.stdin).get('protocol','JSDP'))" <<<"$RESP")"
fi

[[ -n "$CHAIN" ]] || usage

QUEUE_JSON="$(fetch_queue)"

if [[ "$STATUS" == "1" ]]; then
  print_status "$QUEUE_JSON"
  exit 0
fi

if [[ "$CLEANUP_STALE" == "1" ]]; then
  echo "JZ JSDP: stale lease cleanup for chain $CHAIN"
  cleanup_stale "$QUEUE_JSON"
  QUEUE_JSON="$(fetch_queue)"
  print_status "$QUEUE_JSON"
  exit 0
fi

echo "JZ JSDP: chain=$CHAIN"

parse_next() {
  local json="$1"
  local IFS=$'\n'
  local -a lines
  lines=($(read_next "$json"))
  NEXT_SESSION="${lines[0]:-}"
  NEXT_TASK="${lines[1]:-}"
  NEXT_ELIGIBILITY="${lines[2]:-}"
  NEXT_ACTION="${lines[3]:-}"
}

if [[ "$NEXT" == "1" || "$ONCE" == "1" ]]; then
  parse_next "$QUEUE_JSON"

  if [[ "$NEXT_ELIGIBILITY" != "eligible" || -z "$NEXT_SESSION" || -z "$NEXT_TASK" ]]; then
    echo "JZ JSDP: cannot dispatch — convergence gate not satisfied"
    print_status "$QUEUE_JSON"
    exit 1
  fi

  echo "JZ JSDP: dispatch seq — session=$NEXT_SESSION task=$NEXT_TASK"
  "$JZ" task run "$NEXT_TASK" --session "$NEXT_SESSION" --json >/dev/null
  echo "JZ JSDP: dispatched. Next human action after run: accept-merge when ReadyForReview."
  sleep "$DELAY_SEC"
  print_status "$(fetch_queue)"
  exit 0
fi

# Legacy loop mode: wait and dispatch when eligible
while true; do
  QUEUE_JSON="$(fetch_queue)"
  parse_next "$QUEUE_JSON"

    if [[ "$NEXT_ELIGIBILITY" == "eligible" && -n "$NEXT_SESSION" && -n "$NEXT_TASK" ]]; then
    echo "JZ JSDP: dispatch session=$NEXT_SESSION task=$NEXT_TASK"
    if ! "$JZ" task run "$NEXT_TASK" --session "$NEXT_SESSION" --json >/dev/null; then
      echo "JZ JSDP: dispatch failed — inspect queue with --status" >&2
      print_status "$QUEUE_JSON"
      exit 1
    fi
    sleep "$DELAY_SEC"
    continue
  fi

  print_status "$QUEUE_JSON"
  sleep "$POLL_SEC"
done
