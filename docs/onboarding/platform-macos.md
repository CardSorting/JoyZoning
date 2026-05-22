# macOS setup guide

**Reading level:** Beginner — Mac-specific paths and fixes.

JoyZoning desktop and `.app` builds target **macOS arm64** first. Control plane also runs on Apple Silicon and Intel Macs with .NET 8.

---

## What gets installed where

| Item | Typical path |
|------|--------------|
| JoyZoning clone | `~/Projects/JoyZoning` or `~/Desktop/JoyZoning` |
| diet-hermes (default) | `~/Downloads/diet-hermes-main-master` |
| JoyZoning database | `~/Library/Application Support/JoyZoning/joyzoning.db` |
| Onboarding preferences | `~/Library/Application Support/JoyZoning/onboarding.json` |
| Sample workspace | `~/Library/Application Support/JoyZoning/workspaces/getting-started` |
| Hermes profile `joyzoning` | `~/.hermes/profiles/joyzoning/` |
| `jz` CLI | `~/.local/bin/jz` |
| .NET 8 (user install) | `~/.dotnet/` |

**Finder tip:** `~/Library` is hidden — in Finder press **Cmd+Shift+G** and paste the path.

---

## Install .NET 8 (if `dotnet --version` shows 6.x)

```bash
curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0
```

Add to `~/.zshrc`:

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
```

Restart Terminal; verify: `dotnet --version` → 8.0.x

---

## First launch on Mac

```bash
cd ~/Desktop/JoyZoning   # your clone path
./scripts/run-dev.sh
```

**Gatekeeper:** If macOS blocks the app: **System Settings → Privacy & Security → Open Anyway**, or right-click app → **Open**.

**You should see:** Dock icon; setup overlay 0–8 min first time; then Getting Started or Manager Chat.

---

## Release `.app` (without `dotnet run`)

```bash
./scripts/publish-macos.sh
./scripts/bundle-macos-app.sh
open dist/JoyZoning.app
```

Or: `./dist/run-joyzoning.sh`

---

## Mac-specific issues

| Symptom | Fix |
|---------|-----|
| Port in use after crash | `lsof -i :9470` · kill PID |
| `hermes` uses wrong Python | Use `<InstallRoot>/.venv/bin/hermes -p joyzoning` |
| Worktree under `/var/folders/...` | Normal for temp clones; `jz agent` normalizes paths |
| Sleep / lid close | Red chips after wake — **Hermes → Connection → Connect all** |
| Rosetta confusion | Prefer native arm64 .NET; avoid mixing Intel-only venv |

---

## Optional: shell profile for CLI

```bash
cat >> ~/.zshrc <<'EOF'
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
export JOYZONING_URL="http://127.0.0.1:9470"
EOF
source ~/.zshrc
```

Install CLI: `cd JoyZoning && ./scripts/install-jz.sh`

---

## Linux on Mac?

Use Linux in a VM or secondary machine — see [installation.md](installation.md). Docker for JoyZoning itself is not the primary path in MVP docs.

---

## Next

- [quickstart.md](quickstart.md)  
- [troubleshooting-setup.md](troubleshooting-setup.md)  
- [platform-linux.md](platform-linux.md)  

[← Onboarding hub](README.md)
