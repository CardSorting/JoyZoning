# Hermes / diet-hermes integration

JoyZoning does not embed an LLM runtime. It supervises agents through a **local diet-hermes** ([Hermes Agent](https://github.com/NousResearch/hermes-agent)) install and normalizes runs into operator-visible state.

**Setup-focused guide:** [onboarding/hermes-setup.md](onboarding/hermes-setup.md) · [API keys](onboarding/api-keys-and-models.md)

## One install, two roles

```
                    ┌─────────────────────────────┐
                    │  diet-hermes (one checkout) │
                    │  gateway + API :8642        │
                    └──────────────┬──────────────┘
                                   │
              ┌────────────────────┼────────────────────┐
              ▼                    ▼                    ▼
      Manager session        Executor session      Kanban plugin
      (Manager Chat)         (Dispatch/DietCode)   (sync board)
```

| Role | JoyZoning surface | Hermes mechanism |
|------|-------------------|------------------|
| Manager | Manager Chat | `POST /v1/runs` with manager-oriented toolsets |
| Executor | Kanban dispatch → Execution | Same API; bounded toolsets per run |
| Board sync | Kanban import/auto-sync | `/api/plugins/kanban/` via dashboard token |

There is **no** second Hermes tree for “master” vs “slave.”

## Ports and services

| Service | Default | Config key |
|---------|---------|------------|
| Hermes API | `http://127.0.0.1:8642` | `Hermes:ApiBaseUrl` |
| Hermes dashboard | `http://127.0.0.1:9119` | `Hermes:DashboardBaseUrl` |
| JoyZoning control plane | `http://127.0.0.1:9470` | `ControlPlane:ListenUrl` |

## Profile: `joyzoning`

On auto-setup, JoyZoning configures a Hermes profile named **`joyzoning`** with:

- API server enabled on port **8642**
- Generated `API_SERVER_KEY` (stored in Hermes config, not in JoyZoning repo)

You can point to another profile via session creation or Settings if you manage Hermes yourself.

## Install paths

| Method | When |
|--------|------|
| **Auto-setup** (first launch) | Runs `scripts/install-diet-hermes.sh` if no valid install at `Hermes:InstallRoot` |
| **Manual clone** | Clone [diet-hermes](https://github.com/NousResearch/hermes-agent) or your fork; set `InstallRoot` in appsettings or Settings |
| **Existing checkout** | JoyZoning discovers `~/Downloads/diet-hermes-main-master`, `.venv/bin/hermes`, env vars |

After clone, copy [appsettings.example.json](../src/JoyZoning.ControlPlane/appsettings.example.json) to `appsettings.Development.json` and set `Hermes:InstallRoot`.

## Gateway and API health

| API | Purpose |
|-----|---------|
| `GET /api/hermes/health` | Reachability of Hermes API |
| `POST /api/hermes/ensure` | Start gateway when `AutoStartGateway` is true |

Desktop: **Hermes → Ensure Gateway** or Connection window **Connect all**.

Implementation: `HermesConnectivityService`, `HermesProcessService` in `JoyZoning.Agents`.

## Dashboard, token, and TUI

Kanban sync and the embedded Hermes TUI require the **dashboard session token**.

| API | Purpose |
|-----|---------|
| `GET /api/hermes/dashboard` | Reachability + token validity |
| `POST /api/hermes/ensure-dashboard` | Start `hermes dashboard --no-open --tui`, scrape token from HTML |
| `POST /api/hermes/refresh-dashboard-token` | Re-scrape without full ensure |
| `GET /api/hermes/connector-status` | Deep check: API key, health, dashboard, kanban, gateway |

CLI: `jz hermes connector-status` (alias `connectors`).

Flow:

1. Dashboard serves UI on **9119**
2. JoyZoning scrapes `_SESSION_TOKEN` from startup HTML
3. Token is saved in SQLite config for kanban + PTY

**Execution → Connect dashboard & TUI** uses WebSocket `/api/pty?token=…` with [SvcSystems.UI.Terminal](https://www.nuget.org/packages/SvcSystems.UI.Terminal) in the Avalonia app.

## Kanban two-way sync

1. **Import** — `POST /api/tasks/import-kanban` pulls Hermes board into the active session.
2. **Push** — local-only tasks and status changes upload back to Hermes.
3. **Local-wins** — tasks edited in JoyZoning since last sync keep their status on pull.
4. **Auto-sync** — `KanbanAutoSyncHostedService` when enabled in Settings.

Requires valid dashboard token. Status mapping: [architecture.md](architecture.md#task-status-mapping).

## SSE → JoyZoning events

`HermesRunEventConsumer` subscribes to Hermes run SSE and ingests:

- Tool start/complete → execution steps, terminal preview
- Approval requests → Approvals inbox
- Message deltas → Manager Chat stream

Events land in `joy_events` and push over SignalR. Catalog: [event-catalog.md](event-catalog.md).

## What JoyZoning does not call (MVP)

- Firestore / cloud operator APIs
- A second Hermes gateway instance per role
- Remote agent hosts (local loopback only)

## Troubleshooting Hermes

See [troubleshooting.md](troubleshooting.md#hermes-and-connectivity).
