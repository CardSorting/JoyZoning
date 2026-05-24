# diet-hermes setup

JoyZoning supervises **[diet-hermes](https://github.com/NousResearch/hermes-agent)** — a Hermes Agent fork with BroccoliDB, DietCode, and JoyZoning governance. You need **exactly one** install.

**Plain language:** JoyZoning is the **cockpit**; diet-hermes is the **engine**. You do not install a second engine for “manager” vs “worker.”

---

## One install, two roles

```
┌─────────────────────────────────────┐
│  diet-hermes (single folder)        │
│                                     │
│   Profile: joyzoning                │
│   Gateway + API :8642               │
│   Dashboard      :9119              │
│                                     │
│   Session A — Manager (planning)    │
│   Session B — Executor (dispatch)   │
└─────────────────────────────────────┘
           ▲ kanban sync ▲
           │             │
    JoyZoning :9470 (operator UI)
```

| Role | Where you use it | Hermes mechanism |
|------|------------------|------------------|
| **Manager** | Manager Chat | API run with planning toolsets |
| **Executor** | Kanban → Dispatch | API run in canonical workspace (card branch) |
| **Board** | Kanban import/sync | Hermes kanban plugin via dashboard token |

---

## Install methods

### A. JoyZoning auto-setup (easiest)

**Desktop:** first launch overlay or **Getting Started → Run smart setup**

**Script:**

```bash
cd JoyZoning
JOYZONING_DIET_HERMES_DIR="$HOME/Downloads/diet-hermes-main-master" \
  ./scripts/install-diet-hermes.sh
```

**What the script does:**

1. Ensures `uv` (Python package manager)  
2. Creates `.venv` with Python 3.11  
3. Installs `hermes-agent` editable from checkout  
4. Creates profile **`joyzoning`** with API on **8642**  
5. Generates `API_SERVER_KEY` in `~/.hermes/profiles/joyzoning/.env`  
6. Symlinks `~/.local/bin/hermes` to checkout venv (optional)

**Duration:** 3–8 minutes first time; seconds if `.venv` exists.

### B. Manual clone + setup

```bash
git clone https://github.com/NousResearch/hermes-agent.git \
  "$HOME/Downloads/diet-hermes-main-master"
cd "$HOME/Downloads/diet-hermes-main-master"
./setup-hermes.sh
```

Point JoyZoning at this folder:

- `appsettings.Development.json` → `Hermes:InstallRoot`  
- Or desktop **Settings** / **Hermes → Connection… → Browse**

### C. Existing fork checkout

If you already have diet-hermes (e.g. local fork with `plugins/joyzoning_governance/`):

1. Build venv: `uv venv .venv --python 3.11 && uv sync --extra all`  
2. Run profile setup from [install script](../../scripts/install-diet-hermes.sh) (`configure_joyzoning_profile` section) or:

```bash
.venv/bin/hermes -p joyzoning config set API_SERVER_ENABLED true
```

---

## Profile: `joyzoning`

| Setting | Value |
|---------|-------|
| Profile name | `joyzoning` |
| Config dir | `~/.hermes/profiles/joyzoning/` |
| API port | `8642` (env: `API_SERVER_PORT`) |
| Dashboard | `9119` (default Hermes dashboard) |

JoyZoning control plane uses `Hermes:Profile` = `joyzoning` unless you override per session.

### API keys

Copy from your main Hermes setup if you already use one:

```bash
# Example: merge provider keys into joyzoning profile
grep -E '^(OPENROUTER|OPENAI|GOOGLE)_API_KEY=' ~/.hermes/.env >> ~/.hermes/profiles/joyzoning/.env
```

Then:

```bash
.venv/bin/hermes -p joyzoning doctor
.venv/bin/hermes -p joyzoning setup   # interactive if keys missing
```

Details: [api-keys-and-models.md](api-keys-and-models.md).

### Enable governance plugin (recommended for fork)

```bash
.venv/bin/hermes -p joyzoning plugins enable joyzoning_governance
```

Layering checks run on tool use — restart gateway after enabling.

---

## Start / stop services

| Service | Start | Stop |
|---------|-------|------|
| Gateway | `hermes -p joyzoning gateway` | Ctrl+C or kill process |
| Dashboard | `hermes -p joyzoning dashboard --no-open --tui` | Ctrl+C |
| Via JoyZoning | `POST /api/hermes/ensure` · `ensure-dashboard` | — |

Desktop: **Hermes → Ensure Gateway**, **Connection → Connect all**.

---

## Point JoyZoning at your install

| Method | Location |
|--------|----------|
| Developer | `appsettings.Development.json` → `Hermes:InstallRoot` |
| Desktop | Settings → Hermes install path |
| Runtime API | `PUT /api/config` with `installRoot` |
| Env (CLI docs) | `JOYZONING_DIET_HERMES_DIR` for install script only |

**Valid install root** contains:

- `pyproject.toml`  
- `.venv/bin/hermes` or `venv/bin/hermes`  
- For diet-hermes fork: `broccolidb/` or `plugins/joyzoning_governance/`

---

## Verify Hermes from JoyZoning

```bash
curl -s http://127.0.0.1:9470/api/hermes/health
curl -s http://127.0.0.1:9470/api/hermes/dashboard
jz doctor
```

---

## Do not mix installs

| ❌ Avoid | ✅ Instead |
|----------|-----------|
| Global `hermes` from unrelated `~/.hermes/hermes-agent` | Use checkout venv: `<InstallRoot>/.venv/bin/hermes -p joyzoning` |
| Second clone for “executor” | Second **session** on same gateway |
| API keys only in default profile | Keys in `profiles/joyzoning/.env` |

---

## Next

- [API keys & models](api-keys-and-models.md)  
- [Status indicators](status-indicators.md)  
- [Hermes integration (deep dive)](../hermes-integration.md)

[← Onboarding hub](README.md)
