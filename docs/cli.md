# JoyZoning operator CLI (`jz`)

`jz` is the **operator shell** for JoyZoning’s local kanban runtime — human-supervised, scriptable, local-first. It is not a generic HTTP wrapper: workflows preserve the same authority gates as the desktop app (merge-only complete, explicit critical approval, evidence-preserving recovery).

Control plane default: `http://127.0.0.1:9470` (`JOYZONING_URL`).

## Interactive operator TUI (Hermes-style)

**Full strategy:** [hermes-aligned-terminal-strategy.md](hermes-aligned-terminal-strategy.md) — cognition vs authority, why `jz` is not a Hermes clone, governance invariants.

Following the [diet-hermes TUI strategy](https://github.com/NousResearch/hermes-agent/blob/main/AGENTS.md#tui-architecture-ui-tui--tui_gateway): **JoyZoning owns the operator cockpit; Hermes owns agent chat.**

| Surface | Command | Role |
|---------|---------|------|
| **Operator TUI** | `jz` or `jz tui` | Slash commands, tab completion, line history, live tool/event stream (SignalR), Manager Chat, lease workflows |
| **Hermes agent TUI** | `jz hermes tui` or `/hermes` inside `jz tui` | Full `hermes --tui` (multiline agent chat, skills, tools — not reimplemented in .NET) |

```bash
jz                          # auto-starts TUI when stdin is a TTY
jz tui --session <guid>     # pre-bind session / task
jz hermes tui -c            # resume latest Hermes TUI session
JOYZONING_NO_TUI=1 jz task list   # force JSON automation mode
```

**Operator TUI highlights** (aligned with Hermes classic/TUI docs):

- **Slash-command autocomplete** — Tab on `/dis…` etc.; centralized registry like Hermes `COMMAND_REGISTRY`
- **Multiline input** — trailing `\` continues; **Ctrl+G** opens `$EDITOR`
- **Conversation history** — `~/.joyzoning/cli_history`
- **Interrupt-and-redirect** — Ctrl+C stops watch/poll/manager stream first; second Ctrl+C exits
- **Streaming tool output** — `/watch` or Manager messages stream `hermes.tool.*` / `hermes.message.delta` via SignalR `OnJoyEvent`

Plain text (no `/`) sends **Manager Chat** when a session is active. Run `/help` inside the TUI for the full command list.

## Install

```bash
cd /Users/bozoegg/Desktop/JoyZoning
./scripts/install-jz.sh          # ~/.local/bin/jz + checks .NET SDK
./scripts/jz doctor              # without install
eval "$(jz completion bash)"     # optional tab completion
```

## I/O contract (automation)

| Stream | Content |
|--------|---------|
| stdout | JSON on success (machine-readable) |
| stderr | JSON errors (`ok: false`, `error`, `message`) |
| exit `0` | Success |
| exit `1` | API / runtime / local verification failure |
| exit `2` | Usage / network / config |

Flags:

- `--pretty` — indented JSON for humans
- `--quiet` — no stdout on success; use exit codes in scripts
- `--field .id` — print one field (jq-friendly paths: `.status`, `.worktreePath`)
- `--yes` — confirm dangerous operations
- `--approve-critical` — required for critical dispatch/retry (never defaulted)

Environment:

| Variable | Purpose |
|----------|---------|
| `JOYZONING_URL` | Control plane base URL |
| `JOYZONING_SESSION_ID` | Default session for task commands |
| `JOYZONING_TASK_ID` | Default task when omitted |

## Context-aware commands

When cwd is inside a lease **worktree**, or the session has exactly one active lease:

```bash
cd "$WORKTREE"
jz lease
jz heartbeat
jz verify --cmd "dotnet test"
```

## YOLO mode (`jz yolo`)

Bounded **supervised autopilot** — autonomous `jz` inside a policy file, not autonomous authority. Humans still merge.

```bash
jz yolo plan --policy .joyzoning/yolo.policy.json     # dry-run selection
jz yolo run --policy .joyzoning/yolo.policy.json --yes  # execute
jz yolo stop
jz yolo status
```

See [yolo-mode.md](yolo-mode.md). Example policy: `.joyzoning/yolo.policy.example.json`.

## Agent harness (`jz agent`)

Constrained worker surface for DietCode/terminal agents. **Cannot** merge, revoke, or mark tasks Complete.

| Command | Purpose |
|---------|---------|
| `agent start --task <id>` | Bind to lease; write `.joyzoning/context.json` in worktree |
| `agent heartbeat` | Heartbeat + `agent.heartbeat` evidence |
| `agent verify --cmd "..."` | Run cmds in worktree; record pass/fail evidence |
| `agent blocked --reason "..."` | Block with reason |
| `agent done` | Submit verification → **ready_for_review** only |

Golden path (from inside worktree):

```bash
cd "$WORKTREE"
jz agent heartbeat
jz agent verify --cmd "dotnet build" --cmd "dotnet test"
jz agent done   # NOT Complete — human runs: jz task complete <id> --yes
```

Context file (written on dispatch + `agent start`):

`.joyzoning/context.json` — `taskId`, `leaseId`, `sessionId`, `worktreePath`, `controlPlaneUrl`, allowed/forbidden actions, last verification state.

Example scripts: `scripts/examples/agent-happy-path.sh`, `agent-verification-failure.sh`, `human-merge.sh`, `critical-dispatch.sh`.

## Operator workflows

| Command | Purpose |
|---------|---------|
| `task run <id>` | Dispatch + lease snapshot; `--poll 5` `--timeout 600` |
| `task verify <id> --cmd "..."` | Run commands in worktree, submit `VerificationReport` |
| `task complete <id> --yes` | **Human merge only** (never direct Complete status) |
| `task fail <id> --reason "..."` | Record dispatch/execution failure evidence |
| `task recover <id> --mode reopen\|reattach\|replace` | Recovery API; `replace` needs `--yes` |

Critical dispatch:

```bash
jz task dispatch "$TASK" --approve-critical
jz task dispatch-retry "$TASK" --approve-critical --yes
```

## Recipe: happy path

```bash
export JOYZONING_URL=http://127.0.0.1:9470

SESSION=$(jz --field .id session create --name nightly --workspace "$HOME/src/repo")
export JOYZONING_SESSION_ID="$SESSION"

TASK=$(jz --field .id task create --title "Fix CI" --description "Restore green build" --risk 1)

jz task run "$TASK" --poll 10 --timeout 1200
jz task heartbeat "$TASK"

jz task verify "$TASK" \
  --cmd "dotnet build" \
  --cmd "dotnet test tests/JoyZoning.Tests/JoyZoning.Tests.csproj"

jz task complete "$TASK" --yes
```

## Recipe: failure path

```bash
# Dispatch failed (Hermes down) — lease blocked with dispatch.failed
jz task dispatch "$TASK" || true
jz task lease "$TASK" | jq '{status, evidenceLogJson}'

jz task fail "$TASK" --reason "Executor exited non-zero"
jz task recover "$TASK" --mode reopen --yes
jz task dispatch-retry "$TASK" --approve-critical

# Verification failed locally — exit 1, lease stays verifying
jz task verify "$TASK" --cmd "exit 1" || true

# Human revoke
jz task revoke "$TASK" --reason "Wrong branch" --yes
```

## Recipe: critical path

```bash
TASK=$(jz --field .id task create --title "Prod config" --risk 3)

# Without approval → API 403
jz task dispatch "$TASK" && echo unexpected

jz task dispatch "$TASK" --approve-critical
jz task dispatch-retry "$TASK" --approve-critical   # fresh approval each retry
```

## Recipe: CI-style verify (no merge)

```bash
cd "$WORKTREE"
jz verify \
  --cmd "dotnet build src/JoyZoning.Cli/JoyZoning.Cli.csproj" \
  --cmd "dotnet test tests/JoyZoning.Cli.Tests/JoyZoning.Cli.Tests.csproj -q"
# Exit 1 if any command fails; never marks task Complete
```

## Recipe: shell + jq automation

```bash
#!/usr/bin/env bash
set -euo pipefail
export JOYZONING_URL=http://127.0.0.1:9470
export JOYZONING_SESSION_ID="${JOYZONING_SESSION_ID:?}"

STATUS=$(jz --quiet --field .status task lease "$TASK_ID" 2>/dev/null || echo -1)
if [[ "$STATUS" == "4" ]]; then
  jz task complete "$TASK_ID" --yes
fi
```

## Diagnostics

```bash
jz doctor              # .NET SDK, control plane, Hermes hint
jz config explain      # env vars + authority rules
```

## Low-level API (escape hatch)

```bash
jz raw GET api/health
jz raw POST api/tasks/$TASK/verification --body @report.json
echo '{"sessionId":"..."}' | jz raw POST api/tasks/$TASK/lease/heartbeat --body @stdin
```

Dangerous raw calls (`DELETE`, merge/revoke paths) require `--yes`.

## See also

- [hermes-aligned-terminal-strategy.md](hermes-aligned-terminal-strategy.md) — terminal architecture and mental model  
- [README.md](README.md) — documentation index  
- [lease-lifecycle.md](lease-lifecycle.md) — state machine  
- [troubleshooting.md](troubleshooting.md) — CLI exit codes and fixes  
- [glossary.md](glossary.md) — terms  
- [getting-started.md](getting-started.md) — first-run setup  
- [control-plane-api.md](control-plane-api.md) — full REST surface  
- [execution-orchestration-api.md](execution-orchestration-api.md) — lease API  
- [configuration.md](configuration.md) — env vars and paths  
