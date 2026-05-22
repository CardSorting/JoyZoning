# Settings explained

**Reading level:** Beginner — every important toggle in plain language.

Open: **Settings** (gear) in the desktop app. Values save to local SQLite via the control plane — **no cloud**.

---

## Onboarding & launch

| Setting | Default | What it does |
|---------|---------|--------------|
| **Automatic setup on launch** | On | Runs smart setup when app starts (paths, gateway, dashboard) |
| **Prefer Getting Started on launch** | On | Show checklist hub until core steps done |
| **Use sample workspace when needed** | On | Opens tutorial folder if you have not picked a project yet |
| **Auto-install Hermes stack** | On | Downloads/builds diet-hermes if missing at install root |

**When to turn off:** You manage Hermes entirely yourself and want faster launches.

---

## Hermes connection

| Setting | What it does |
|---------|--------------|
| **Hermes install path** | Folder with `pyproject.toml` and `.venv/bin/hermes` |
| **API base URL** | Usually `http://127.0.0.1:8642` — change only if you customized Hermes |
| **Dashboard base URL** | Usually `http://127.0.0.1:9119` |
| **Profile name** | Default `joyzoning` — separate config from personal Hermes |
| **Auto-connect on startup** | Attempt gateway + dashboard when app opens |
| **Auto-start gateway** | Control plane may spawn `hermes gateway` when API down |

**Dashboard session token (Advanced):** Leave empty for automatic scrape. Paste only if auto-connect fails.

---

## Kanban sync

| Setting | What it does |
|---------|--------------|
| **Auto-sync enabled** | Periodically import Hermes kanban into active session |
| **Auto-sync interval** | Seconds between imports (e.g. 120) |

**Requires:** Green **Dashboard** chip (valid token).

**Behavior:** Two-way sync — local edits push back; JoyZoning wins conflicts on status for cards you touched. [hermes-integration.md](../hermes-integration.md)

---

## Developer-oriented (still in UI)

| Setting | What it does |
|---------|--------------|
| **Copy health report** | JSON/text bundle for support — no API keys |
| **Reset onboarding** | Clears `onboarding.json` — checklist reappears; **keeps tasks** |

---

## What is *not* in Settings

| Item | Where instead |
|------|---------------|
| LLM API keys | `~/.hermes/profiles/joyzoning/.env` — [api-keys-and-models.md](api-keys-and-models.md) |
| Model choice | `hermes -p joyzoning model` |
| Lease timeouts | `appsettings` / [configuration.md](../configuration.md) `LeaseRuntime` |

---

## Recommended profiles

### “I just want it to work”

- Automatic setup on launch: **On**  
- Auto-connect: **On**  
- Auto-sync: **On** after first successful dashboard connect  

### “I am a power user”

- Turn off sample workspace when using real repo  
- Set install path explicitly to your fork  
- Manual gateway in Terminal if you debug Hermes often  

### “Corporate locked machine”

- Turn off auto-install; IT provides diet-hermes path  
- Paste dashboard token manually if scrape blocked  

---

## Changes apply immediately

Hermes HTTP clients reload from saved config — **no control plane restart** for URL/token changes.

If UI still looks stale: restart desktop app only.

---

## Next

- [status-indicators.md](status-indicators.md)  
- [configuration.md](../configuration.md) — file-level reference  

[← Onboarding hub](README.md)
