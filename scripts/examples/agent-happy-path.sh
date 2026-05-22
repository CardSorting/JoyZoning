#!/usr/bin/env bash
# Golden agent path: heartbeat → verify → done (ready for human review, NOT Complete)
set -euo pipefail

: "${JOYZONING_URL:=http://127.0.0.1:9470}"
: "${TASK_ID:?Set TASK_ID to a dispatched task}"

export JOYZONING_URL

WORKTREE=$(jz --field worktreePath agent start --task "$TASK_ID")
cd "$WORKTREE"

jz agent heartbeat
jz agent verify \
  --cmd "dotnet build src/JoyZoning.Cli/JoyZoning.Cli.csproj" \
  --cmd "dotnet test tests/JoyZoning.Cli.Tests/JoyZoning.Cli.Tests.csproj -q"
jz agent done

echo "Lease is ready_for_review. Human merge: jz task complete $TASK_ID --yes"
