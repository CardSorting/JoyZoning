# Installation guide

Complete install reference for JoyZoning and its dependency **diet-hermes** (Hermes Agent fork).

**Quick path:** [quickstart.md](quickstart.md) · **Hermes only:** [hermes-setup.md](hermes-setup.md) · **Cursor-first (no Hermes):** [execution-paths.md](../execution-paths.md) · [setup-checklist.md](setup-checklist.md) (steps 1 + 5)

---

## What you are installing

| Piece | What it is (plain language) | Required for |
|-------|----------------------------|--------------|
| **JoyZoning desktop** | Operator window (kanban, chat, diffs) | All paths |
| **Control plane** | Small local web API on port 9470 | All paths |
| **diet-hermes** | AI agent runtime (gateway + tools); first venv **3–8 min** | **Managed** dispatch + Manager Chat only |
| **`jz` CLI** | Terminal commands against 9470 | External JSDP + automation (recommended) |

All components stay on **127.0.0.1** — local-first.

---

## System requirements

| Requirement | macOS | Linux |
|-------------|-------|-------|
| OS | 12+ recommended (arm64 for `.app`) | Recent distro |
| **.NET 8 SDK** | Required (`global.json` pins 8.0.421) | Same |
| **git**, **curl** | For Hermes install script | Same |
| **Python 3.11** | Installed by `uv` during Hermes setup | Same |
| Disk | ~500 MB for venv + NuGet caches | Similar |

---

## .NET 8 on macOS

JoyZoning requires **.NET 8**, not the system 6.x often preinstalled.

```bash
# Check
dotnet --version   # should start with 8.

# If you see 6.x, install SDK 8 and prefer user install:
curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
```

Add those `export` lines to `~/.zshrc` so Terminal and JoyZoning agree.

**You should see:** `dotnet --version` → `8.0.4xx` (or newer 8.x).

---

## Install JoyZoning (developers)

### 1. Clone

```bash
git clone https://github.com/CardSorting/JoyZoning.git
cd JoyZoning
```

### 2. Local configuration (recommended)

```bash
cp src/JoyZoning.ControlPlane/appsettings.example.json \
   src/JoyZoning.ControlPlane/appsettings.Development.json
```

Edit **only** `appsettings.Development.json`:

```json
"Hermes": {
  "InstallRoot": "/absolute/path/to/diet-hermes-main-master"
}
```

Use your real path — e.g. `~/Downloads/diet-hermes-main-master`.  
The committed `appsettings.json` keeps a placeholder; never commit secrets there.

### 3. Run desktop + control plane

```bash
./scripts/run-dev.sh
```

Or separately:

```bash
# Terminal A
export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH"
dotnet run --project src/JoyZoning.ControlPlane

# Terminal B
dotnet run --project src/JoyZoning.App
```

**You should see:** Control plane log `Now listening on: http://127.0.0.1:9470`.

### 4. Optional: install `jz`

```bash
./scripts/install-jz.sh
source scripts/jz-env.sh   # or add exports to ~/.zshrc
jz doctor
```

**You should see:** `"ok": true` for control plane and Hermes API (after gateway is up).

---

## Install diet-hermes (automatic or manual)

### Automatic (desktop smart setup / script)

```bash
cd JoyZoning
JOYZONING_DIET_HERMES_DIR="$HOME/Downloads/diet-hermes-main-master" \
  ./scripts/install-diet-hermes.sh
```

Creates:

- `.venv` in the checkout  
- Hermes profile **`joyzoning`** at `~/.hermes/profiles/joyzoning/`  
- API on port **8642** with generated `API_SERVER_KEY`  

Details: [hermes-setup.md](hermes-setup.md).

### Manual (existing checkout)

```bash
cd /path/to/diet-hermes-main-master
./setup-hermes.sh    # or: uv venv .venv && uv sync --extra all
```

Then point JoyZoning `InstallRoot` at that folder.

---

## macOS release build (operators without `dotnet run`)

```bash
./scripts/publish-macos.sh
./dist/run-joyzoning.sh
# Optional .app:
./scripts/bundle-macos-app.sh
open dist/JoyZoning.app
```

---

## Verify installation

| Check | Command / UI |
|-------|----------------|
| Control plane | `curl -s http://127.0.0.1:9470/api/health` |
| Hermes API | `curl -s http://127.0.0.1:8642/health` |
| CLI bundle | `jz doctor` |
| Desktop | Getting Started → health grade **Healthy** |

---

## Common install mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| .NET 6 on PATH | Build fails / wrong runtime | [`.NET 8 on macOS`](#net-8-on-macos) |
| Wrong `InstallRoot` | “hermes not found” | Browse path in Settings; must contain `.venv/bin/hermes` |
| Two Hermes installs | Confusing API keys | Use **one** tree; profile `joyzoning` — [hermes-setup.md](hermes-setup.md) |
| `jz` symlink overwrote binary | `cannot execute binary file` | Re-run `./scripts/install-jz.sh` (wrapper + binary separate) |
| Port 9470 busy | CP won’t start | `lsof -i :9470` · stop old process |

---

## Uninstall / reset (local only)

| What | Action |
|------|--------|
| JoyZoning state | Delete `~/Library/Application Support/JoyZoning/` (macOS) |
| Onboarding only | Desktop **Settings → Reset onboarding** |
| Hermes profile | Remove `~/.hermes/profiles/joyzoning/` (keeps default profile) |
| diet-hermes venv | Delete `<checkout>/.venv` |

---

## Next

- [First run (desktop)](first-run-desktop.md)  
- [First run (CLI)](first-run-cli.md)  
- [Configuration reference](../configuration.md)

[← Onboarding hub](README.md)
