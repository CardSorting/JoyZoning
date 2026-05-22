# First run (terminal / `jz`)

Run JoyZoning **without the desktop** — same governance rules, scriptable interface.

**Familiar pattern:** Like `gh pr create` or `docker compose up` talking to a local daemon.

**Prerequisites:** [installation.md](installation.md) (control plane + diet-hermes)

---

## Architecture (one picture)

```
jz  →  http://127.0.0.1:9470  →  SQLite + orchestrator
              ↓
         hermes -p joyzoning gateway  →  :8642 / :9119
```

The control plane must be running **before** `jz` commands (desktop auto-starts it; CLI-only users start it manually).

---

## One-time setup

### 1. Shell environment

Add to `~/.zshrc` (recommended):

```bash
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="${DOTNET_ROOT}:${PATH}"
export JOYZONING_URL="${JOYZONING_URL:-http://127.0.0.1:9470}"
export JOYZONING_DIET_HERMES_DIR="${JOYZONING_DIET_HERMES_DIR:-$HOME/Downloads/diet-hermes-main-master}"
```

Or per session:

```bash
source /path/to/JoyZoning/scripts/jz-env.sh
```

### 2. Install CLI

```bash
cd JoyZoning
./scripts/install-jz.sh
```

**You should see:** `Installed: ~/.local/bin/jz`

> **Note:** `jz` is a small wrapper script; the .NET app lives in `~/.local/bin/joyzoning-jz/`. Do not replace the binary with the wrapper manually.

### 3. Start control plane

```bash
export ASPNETCORE_ENVIRONMENT=Development
cd JoyZoning
dotnet run --project src/JoyZoning.ControlPlane/JoyZoning.ControlPlane.csproj
```

Leave this terminal open.

### 4. Start Hermes gateway

```bash
"$JOYZONING_DIET_HERMES_DIR/.venv/bin/hermes" -p joyzoning gateway
```

Optional dashboard (kanban sync):

```bash
hermes -p joyzoning dashboard --no-open --tui
```

Or: `curl -X POST http://127.0.0.1:9470/api/hermes/ensure-dashboard`

### 5. Doctor

```bash
jz doctor
```

**You should see:**

```json
{"ok": true, "checks": [
  {"id": "control_plane", "status": "ok"},
  {"id": "hermes_api", "status": "ok"}
]}
```

---

## Your first session and task

```bash
# Bind to a project folder (like opening a folder in VS Code)
SESSION=$(jz --field .id session create \
  --name "cli-first-run" \
  --workspace "$HOME/src/myrepo")
export JOYZONING_SESSION_ID="$SESSION"

# Create a low-risk task
TASK=$(jz --field .id task create \
  --title "CLI smoke test" \
  --description "Prove jz wiring" \
  --risk 1)
echo "TASK=$TASK"
```

**You should see:** UUIDs printed; no stderr errors.

---

## Dispatch and poll (operator)

```bash
jz task run "$TASK" --poll 10 --timeout 600
```

**You should see:** JSON status transitions (`leased` → `running` → …).  
If Hermes is down: exit `1` and JSON `message` on stderr — start gateway.

---

## Verify and merge (human-only complete)

```bash
jz task verify "$TASK" --cmd "echo ok"
jz task complete "$TASK" --yes
```

**You should see:** Task reaches **Complete** only after `--yes` merge.

Agents use `jz agent done` → **ready_for_review**, not Complete. See [cli.md](../cli.md).

---

## Agent harness (inside worktree)

After dispatch:

```bash
WORKTREE=$(jz --field .worktreePath task lease "$TASK")
cd "$WORKTREE"
jz agent start --task "$TASK"
jz agent verify --cmd "dotnet build"
jz agent done
# You still run: jz task complete "$TASK" --yes
```

Examples: `scripts/examples/agent-happy-path.sh`

---

## CLI vs desktop feature map

| Desktop | CLI equivalent |
|---------|----------------|
| Getting Started health | `jz doctor` |
| Open workspace | `session create --workspace` |
| Kanban dispatch | `task run` |
| Merge button | `task complete --yes` |
| Copy health report | `jz raw GET api/health` + curls |

---

## Troubleshooting CLI-first

| Symptom | Fix |
|---------|-----|
| `Session required` | `export JOYZONING_SESSION_ID=...` |
| `.NET 8` missing | `DOTNET_ROOT` — [installation.md](installation.md) |
| `cannot execute binary file` | Re-run `./scripts/install-jz.sh` |
| Hermes API warn in doctor | Start gateway; [hermes-setup.md](hermes-setup.md) |

---

## Next

- [CLI reference](../cli.md)  
- [whats-next.md](whats-next.md)  
- [choose-your-path.md](choose-your-path.md)

[← Onboarding hub](README.md)
