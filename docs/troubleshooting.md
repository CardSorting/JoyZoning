# Troubleshooting

Symptom-first guide for operators and developers.

| Situation | Doc |
|-----------|-----|
| **Setup failed (decision trees)** | **[onboarding/troubleshooting-setup.md](onboarding/troubleshooting-setup.md)** |
| Red chips / health grade | [onboarding/status-indicators.md](onboarding/status-indicators.md) |
| Step-by-step setup | [onboarding/setup-checklist.md](onboarding/setup-checklist.md) |
| Menu “where to click” | [onboarding/desktop-menu-guide.md](onboarding/desktop-menu-guide.md) |
| Overview | [getting-started.md](getting-started.md) |

## Quick diagnostics

```bash
# CLI
jz doctor
jz config explain

# Control plane (separate terminal)
curl -s http://127.0.0.1:9470/api/health
curl -s http://127.0.0.1:9470/api/hermes/health
curl -s http://127.0.0.1:9470/api/hermes/dashboard
```

Desktop: **Settings → Copy health report** (paste into issues or notes).

## Hermes and connectivity

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| API chip red | Gateway not running or wrong URL | **Hermes → Ensure Gateway**; check `Hermes:ApiBaseUrl` |
| Dashboard chip red | Dashboard not started | **Connection → Connect dashboard** or `POST /api/hermes/ensure-dashboard` |
| Kanban import empty | Missing/invalid token | Connect dashboard; check Settings → Advanced token |
| First launch stuck | Install/build in progress | Wait 3–8 min; check overlay log; retry **Smart setup** from Getting Started |
| Wrong Hermes tree | Bad `InstallRoot` | Settings or `appsettings.Development.json` → path to diet-hermes root with `.venv` or `venv` |
| `hermes` not found | venv not activated / not built | Run `scripts/install-diet-hermes.sh` or manual `pip install` in checkout |

## Control plane

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| Desktop cannot connect | CP not on :9470 | App auto-starts CP; or `dotnet run --project src/JoyZoning.ControlPlane` |
| Port in use | Another process on 9470 | Stop duplicate CP; change `ControlPlane:ListenUrl` |
| Stale UI after Settings save | Rare client cache | Settings hot-reload Hermes clients; restart desktop only if needed |

## Kanban and leases

| Symptom | HTTP / behavior | Fix |
|---------|-----------------|-----|
| Dispatch 403 | `lease_forbidden` | Critical card: enable approval checkbox or `--approve-critical` |
| Dispatch 409 | `lease_conflict` | Another critical lease active; revoke/complete other card first |
| Cannot drag to Complete | Lease not `ready_for_review` | Run verification; use **merge** not raw status |
| Stuck in Verifying | Failed verify | Re-run `jz task verify` or `jz agent verify`; check command output |
| Worktree missing | Reconciliation blocked lease | `task recover --mode reopen` or reattach; see [execution-orchestration-api.md](execution-orchestration-api.md) |
| Agent `done` fails | No passing verify | Run verify first; all `--cmd` must pass |

## CLI (`jz`)

| Symptom | Exit code | Fix |
|---------|-----------|-----|
| Usage error | 2 | Run `jz --help`; check required `--session`, `--yes`, `--approve-critical` |
| API error | 1 | Read stderr JSON `message`; ensure CP URL via `JOYZONING_URL` |
| Verify failed | 1 | Commands failed locally; lease stays verifying |
| Multiple leases match cwd | 2 | Pass explicit task id |
| Agent blocked on merge | 2 | Use `jz task complete` (human), not `jz agent done` for Complete |

**macOS path note:** worktrees under `/var/folders/...` are normalized for `jz agent` guard checks. If you symlink worktrees, stay inside the lease path.

## Executions and recovery

| Symptom | Fix |
|---------|-----|
| “Interrupted” on restart | **Recovery** menu → resume or dismiss (`POST .../resume` or `cancel`) |
| Orphan running lease | Background reconciliation blocks lease; check Timeline for `reconciliation.*` evidence |
| Hermes run died, lease still running | `task fail` or wait for reconciliation; then recover |

## Tests and development

| Symptom | Fix |
|---------|-----|
| `JoyZoning.Tests` not in solution | Run by path: `dotnet test tests/JoyZoning.Tests/...` |
| Dogfood fails on `jz` not found | Build CLI first: `dotnet build src/JoyZoning.Cli` |
| Tests write to real DB | Set `JOYZONING_DB_PATH` to temp file (see `tests` infrastructure) |

## Logs and data locations

| Platform | Path |
|----------|------|
| macOS DB | `~/Library/Application Support/JoyZoning/joyzoning.db` |
| macOS onboarding | `~/Library/Application Support/JoyZoning/onboarding.json` |
| Lease worktrees | `<workspace>/.joyzoning/worktrees/<task-id>/` |

To reset onboarding only: **Settings → Reset onboarding** (does not delete tasks DB).

## Still stuck?

1. Capture health report from the desktop.
2. Note JoyZoning version/commit: `git rev-parse --short HEAD`
3. Note Hermes profile and whether API/dashboard curls succeed.
4. Open an issue: [github.com/CardSorting/JoyZoning/issues](https://github.com/CardSorting/JoyZoning/issues)
