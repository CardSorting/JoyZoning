# Linux setup guide

**Reading level:** Beginner–Operator.

JoyZoning **control plane** and **`jz` CLI** run on Linux. The Avalonia **desktop** is developable on Linux; the polished `.app` experience is macOS-first.

**Recommended Linux path:** control plane + `jz` + optional browser; or desktop from source if you need GUI.

---

## Supported workflow

| Component | Linux |
|-----------|-------|
| Control plane `:9470` | Yes |
| `jz` / `jz agent` | Yes |
| diet-hermes gateway | Yes |
| JoyZoning.App (Avalonia) | Yes from source (`dotnet run`) |
| Prebuilt `.app` | No (macOS arm64) |

---

## Prerequisites

```bash
# .NET 8 SDK — Ubuntu/Debian example
wget https://dot.net/v1/dotnet-install.sh
bash dotnet-install.sh --channel 8.0
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"

# git, curl for Hermes install
sudo apt install -y git curl   # or distro equivalent
```

---

## Install sequence

```bash
git clone https://github.com/CardSorting/JoyZoning.git
cd JoyZoning

cp src/JoyZoning.ControlPlane/appsettings.example.json \
   src/JoyZoning.ControlPlane/appsettings.Development.json
# Edit Hermes:InstallRoot in Development.json

JOYZONING_DIET_HERMES_DIR="$HOME/diet-hermes-main-master" \
  ./scripts/install-diet-hermes.sh
```

### Terminal A — control plane

```bash
export ASPNETCORE_ENVIRONMENT=Development
dotnet run --project src/JoyZoning.ControlPlane
```

### Terminal B — Hermes

```bash
$HOME/diet-hermes-main-master/.venv/bin/hermes -p joyzoning gateway
```

### Terminal C — CLI (optional)

```bash
./scripts/install-jz.sh
source scripts/jz-env.sh
jz doctor
```

### Desktop (optional)

```bash
dotnet run --project src/JoyZoning.App
```

---

## Linux data paths

| Item | Path |
|------|------|
| Database | `~/.local/share/JoyZoning/` or overridden by `JOYZONING_DB_PATH` |
| Hermes profile | `~/.hermes/profiles/joyzoning/` |

(Exact Application Support mapping follows XDG; macOS uses `~/Library/Application Support/`.)

---

## Linux-specific issues

| Symptom | Fix |
|---------|-----|
| `dotnet` 6 from distro | Install user-wide .NET 8 — do not rely on `apt install dotnet-sdk-6` |
| Firewall blocking localhost | Allow `127.0.0.1` ports 9470, 8642, 9119 |
| Wayland + Avalonia | If UI glitches, try X11 session or CLI-only path |
| Case-sensitive paths | `InstallRoot` must match exact disk casing |

---

## Next

- [first-run-cli.md](first-run-cli.md)  
- [installation.md](installation.md)  
- [platform-macos.md](platform-macos.md)  

[← Onboarding hub](README.md)
