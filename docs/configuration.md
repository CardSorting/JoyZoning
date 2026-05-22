# Configuration

JoyZoning is **local-first**: all state and settings stay on your machine. There is no cloud control plane.

## Configuration layers

| Layer | Location | Used for |
|-------|----------|----------|
| **appsettings.json** | `src/JoyZoning.ControlPlane/appsettings.json` | Committed defaults (placeholder `InstallRoot`) |
| **appsettings.example.json** | Same directory | Copy template with documented `LeaseRuntime` block |
| **appsettings.Development.json** | Same directory (gitignored) | **Your** machine: real `InstallRoot`, local overrides |
| **SQLite `app_config`** | Inside `joyzoning.db` | Settings saved from the desktop UI (`PUT /api/config`) |
| **Environment variables** | Shell / `.env` | CLI (`JOYZONING_*`), optional DB override, Hermes hints |
| **onboarding.json** | Application Support | First-run checklist, surface tips, last workspace |

Runtime Hermes HTTP clients reload from saved config **without** restarting the control plane.

## Default ports and URLs

| Service | Setting key | Default |
|---------|-------------|---------|
| Control plane | `ControlPlane:ListenUrl` | `http://127.0.0.1:9470` |
| Hermes API | `Hermes:ApiBaseUrl` | `http://127.0.0.1:8642` |
| Hermes dashboard | `Hermes:DashboardBaseUrl` | `http://127.0.0.1:9119` |
| Hermes profile | `Hermes:Profile` | `joyzoning` |

## First-time developer setup

```bash
cp src/JoyZoning.ControlPlane/appsettings.example.json \
   src/JoyZoning.ControlPlane/appsettings.Development.json
```

Edit `Hermes:InstallRoot` in **Development** only. ASP.NET Core merges `appsettings.json` + `appsettings.Development.json` when `ASPNETCORE_ENVIRONMENT=Development`.

## appsettings.json (checked in)

```json
{
  "Hermes": {
    "InstallRoot": "/path/to/diet-hermes-main-master",
    "Profile": "joyzoning",
    "ApiBaseUrl": "http://127.0.0.1:8642",
    "DashboardBaseUrl": "http://127.0.0.1:9119",
    "AutoStartGateway": true
  },
  "ControlPlane": {
    "ListenUrl": "http://127.0.0.1:9470"
  }
}
```

`Hermes:AutoStartGateway` — when true, `POST /api/hermes/ensure` can spawn the gateway process.

### Lease runtime (`LeaseRuntime` section)

Optional; defaults come from `LeaseRuntimeOptions` in `JoyZoning.Domain`:

| Key | Default | Role |
|-----|---------|------|
| `MaxGlobalActiveLeases` | 16 | Cap concurrent active leases |
| `MaxActiveLeasesPerSession` | 8 | Per operator session |
| `MaxCriticalLeases` | 1 | Global critical slot |
| `ReconciliationIntervalSeconds` | 60 | Background reconciliation period |
| `AbsoluteExpirationRevokes` | false | Past `ExpiresAt` → revoked vs blocked |
| `Duration.LowHours` | 8 | Lease TTL by risk |
| `Duration.MediumHours` | 4 | |
| `Duration.CriticalHours` | 2 | |
| `Stale.LeasedMinutes` | 30 | Heartbeat staleness thresholds |
| `Stale.RunningMinutes` | 45 | |
| `Stale.VerifyingMinutes` | 90 | |
| `Stale.CriticalLeasedMinutes` | 10 | Shorter for critical cards |
| `Stale.CriticalRunningMinutes` | 20 | |
| `Stale.CriticalVerifyingMinutes` | 45 | |

Example override in `appsettings.Development.json`:

```json
{
  "LeaseRuntime": {
    "MaxCriticalLeases": 1,
    "Stale": { "RunningMinutes": 30 }
  }
}
```

## Data paths

| Data | macOS path | Override |
|------|------------|----------|
| SQLite database | `~/Library/Application Support/JoyZoning/joyzoning.db` | `JoyZoning:DatabasePath` or `JOYZONING_DB_PATH` |
| Onboarding state | `~/Library/Application Support/JoyZoning/onboarding.json` | — |
| Sample workspace | `~/Library/Application Support/JoyZoning/workspaces/getting-started` | Created when no project open |
| Lease worktrees | Under session workspace: `.joyzoning/worktrees/<task-id>/` | Sandbox enforced by orchestrator |
| Agent context file | `<worktree>/.joyzoning/context.json` | Written on dispatch / `jz agent start` |

## Environment variables

### CLI / automation

| Variable | Default | Purpose |
|----------|---------|---------|
| `JOYZONING_URL` | `http://127.0.0.1:9470` | Control plane base URL for `jz` |
| `JOYZONING_SESSION_ID` | — | Default session for task commands |
| `JOYZONING_TASK_ID` | — | Default task id when omitted |
| `JOYZONING_DB_PATH` | — | SQLite path (tests, dogfood) |

### Hermes (documentation / manual setup)

See [.env.example](../.env.example):

| Variable | Purpose |
|----------|---------|
| `HERMES_INSTALL_ROOT` | diet-hermes checkout path |
| `HERMES_API_URL` | API base (8642) |
| `HERMES_DASHBOARD_URL` | Dashboard base (9119) |
| `HERMES_PROFILE` | Hermes profile name (`joyzoning`) |
| `HERMES_DASHBOARD_SESSION_TOKEN` | Optional manual token for kanban + TUI |

The desktop **Settings** UI and `PUT /api/config` are the preferred way to persist Hermes connection values.

## Single diet-hermes install

JoyZoning deliberately uses **one** Hermes tree:

- Default: `~/Downloads/diet-hermes-main-master`
- Installed by `scripts/install-diet-hermes.sh` on first auto-setup
- Manager and executor are **different sessions** on the same gateway, synced via kanban

There is no second “slave” Hermes install in the product model.

## Kanban sync settings (UI → SQLite)

Saved via Settings and exposed on `GET /api/config`:

- **Auto-import** — periodic `POST /api/tasks/import-kanban`
- **Interval** — seconds between syncs
- **Dashboard token** — required for Hermes kanban plugin API

Two-way sync: import pulls Hermes board; local-only tasks and status changes push back. **Local-wins** on pull for tasks edited in JoyZoning since last sync.

## Testing configuration

When `ASPNETCORE_ENVIRONMENT=Testing`:

- Control plane does not bind `ListenUrl` (in-proc `WebApplicationFactory` or dogfood subprocess sets port)
- `TestAgentHostSetup` replaces real Hermes adapters with stubs
- Tests set `JOYZONING_DB_PATH` to an isolated SQLite file
