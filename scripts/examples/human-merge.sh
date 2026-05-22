#!/usr/bin/env bash
# Human-only merge after agent done (ready_for_review)
set -euo pipefail

: "${JOYZONING_URL:=http://127.0.0.1:9470}"
: "${TASK_ID:?Set TASK_ID}"

export JOYZONING_URL

STATUS=$(jz --field .status task lease "$TASK_ID")
if [[ "$STATUS" != "4" ]]; then
  echo "Lease status must be ready_for_review (4), got: $STATUS" >&2
  exit 1
fi

jz task complete "$TASK_ID" --yes
echo "Human merge complete."
