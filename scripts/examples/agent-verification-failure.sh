#!/usr/bin/env bash
# Agent verification failure — must NOT reach ready_for_review or Complete
set -euo pipefail

: "${JOYZONING_URL:=http://127.0.0.1:9470}"
: "${TASK_ID:?Set TASK_ID}"

export JOYZONING_URL
jz agent start --task "$TASK_ID"
WORKTREE=$(jz --field worktreePath lease "$TASK_ID")
cd "$WORKTREE"

jz agent heartbeat
if jz agent verify --cmd "false"; then
  echo "unexpected: verify should fail locally" >&2
  exit 1
fi

if jz agent done 2>/dev/null; then
  echo "unexpected: done must not succeed after failed verify" >&2
  exit 1
fi

echo "OK: failed verify blocked done"
