# Status indicators

Plain-language guide to **health grade**, **status bar chips**, and what to do when they are not green.

Pattern: similar to **Docker Desktop** (engine running / not) and **VS Code** (Problems panel severity).

---

## Health grade (Getting Started hub)

| Grade | Meaning | What you can do |
|-------|---------|-----------------|
| **Healthy** | All 5 core checklist steps complete | Full workflow: dispatch, merge, sync |
| **Degraded** | Core OK but optional milestone missing | Work normally; optional steps suggested |
| **Blocked** | One or more core steps failed | Fix checklist before dispatch |

Refresh: open **Getting Started** or run **Smart setup**.

---

## Status bar chips

Shown at the top of the desktop app:

```
[ API ● ]  [ Dashboard ● ]  …
```

| Chip | Green | Red / gray |
|------|-------|------------|
| **API** | Hermes gateway answering on configured URL (default `:8642`) | Gateway not running or wrong `InstallRoot` |
| **Dashboard** | Dashboard up **and** session token valid for kanban/TUI | Dashboard stopped or token stale |

**Click either chip** → opens **Hermes → Connection…**

---

## Behind the chips (for support)

| Chip | JoyZoning endpoint |
|------|-------------------|
| API | `GET /api/hermes/health` |
| Dashboard | `GET /api/hermes/dashboard` |

CLI equivalent:

```bash
curl -s http://127.0.0.1:9470/api/hermes/health
curl -s http://127.0.0.1:9470/api/hermes/dashboard
jz doctor
```

---

## Fix playbook (symptom-first)

### API chip red

1. **Hermes → Ensure Gateway**  
2. If still red: **Getting Started → Run smart setup**  
3. Confirm install path has `.venv/bin/hermes`  
4. Manual: `hermes -p joyzoning gateway` in Terminal  

### Dashboard chip red

1. **Connection → Connect dashboard**  
2. Wait ~15s for token scrape  
3. If kanban still empty: **Refresh token** in Connection → Advanced  
4. Restarted `hermes dashboard`? Reconnect once  

### Both red after sleep / reboot

Normal — local services stop. **Connect all** or smart setup restores them.

---

## Kanban column colors (task state)

Not the same as health chips — these are **work status**:

| Column | Meaning |
|--------|---------|
| Backlog / Planned | Not running yet |
| In Progress | Dispatched; lease active |
| Needs Approval | Critical dispatch awaiting checkbox |
| Verifying | Tests/commands running |
| Blocked | Failed dispatch, stale worker, or agent blocked |
| Complete | Human merged after verification |

State machine: [lease-lifecycle.md](../lease-lifecycle.md).

---

## Timeline severity (audit)

Events are informational — look for:

| Event families | Usually means |
|----------------|---------------|
| `hermes.tool.*` | Agent using tools |
| `approval.*` | Risky action needs you |
| `lease.*` / `reconciliation.*` | Governance transitions |
| `dispatch.failed` | Gateway/Hermes error — check API chip |

---

## “Copy health report”

**Settings → Copy health report** bundles:

- Control plane reachability  
- Hermes API/dashboard state  
- Install paths (no API keys)  

Paste into GitHub issues when asking for help.

---

## Next

- [Setup checklist](setup-checklist.md)  
- [Troubleshooting](../troubleshooting.md)  
- [First run (desktop)](first-run-desktop.md)

[← Onboarding hub](README.md)
