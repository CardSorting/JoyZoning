#!/usr/bin/env bash
# Critical card dispatch requires explicit human approval flag
set -euo pipefail

: "${JOYZONING_URL:=http://127.0.0.1:9470}"
: "${TASK_ID:?Set TASK_ID (critical risk task)}"

export JOYZONING_URL

if jz --quiet task dispatch "$TASK_ID"; then
  echo "unexpected: critical dispatch without approval succeeded" >&2
  exit 1
fi

jz task dispatch "$TASK_ID" --approve-critical
jz agent start --task "$TASK_ID"
echo "Critical dispatch OK with --approve-critical"
