# Onboarding and Setup Guide

JoyZoning is the **habitat** (observe, review, accept-merge). **Hermes** is the **runtime** (execution, journal, gates) and must be installed separately.

See [Hermes runtime reversal](architecture/hermes-runtime-reversal.md) before changing integration code.

## Prerequisites

- **Node.js** v20+ and **pnpm** v9+
- **.NET 8.0 SDK** (control plane + desktop)
- **Python 3.11+** for your **external** Hermes checkout (not inside this repo)

---

## One-touch setup

### 1. Install JS dependencies

```bash
pnpm install
```

### 2. Run setup wizard

```bash
pnpm setup
```

The wizard:

- Checks Node, pnpm, and .NET SDK versions
- Verifies ports `9470` (control plane) and `3000` (Watch) are free
- Creates `.env` from `.env.example` when missing
- Initializes `.joy-workspaces/default/`
- Builds the `agent-runtime` tombstone package (410 stub on `:9090`)

It does **not** install a Python venv under `apps/agent-runtime/` — that vendored tree was removed.

### 3. Configure external Hermes

Copy `.env.example` to `.env` and set:

```bash
HERMES_INSTALL_ROOT=/path/to/diet-hermes-main-master
HERMES_API_URL=http://127.0.0.1:8642
```

In the Hermes checkout:

```bash
source .venv/bin/activate   # or venv/
hermes setup                # API keys → ~/.hermes/.env
```

In `~/.hermes/config.yaml` (JoyZoning integration):

```yaml
joyzoning:
  enabled: true
  emit_habitat_events: true
  control_plane:
    url: http://127.0.0.1:9470
    observe_only: true
```

Set matching tokens for ingest/bridge (control plane `InternalToken`).

### 4. Run the development stack

```bash
pnpm dev
```

Starts:

1. **Control plane** — `9470`
2. **Watch UI** — `3000`
3. **Desktop app** — after a short delay

LegacyRuntimeShim (`:9090`) is **not** started. If you hit it accidentally, it returns **410 Gone** with migration JSON.

---

## Configuration and secrets

| Secret / setting | Where |
|------------------|--------|
| Provider API keys | `~/.hermes/.env` (Hermes home) |
| JoyZoning ↔ Hermes tokens | Control plane `InternalToken`, env `JOYZONING_INGEST_TOKEN`, `JOYZONING_HABITAT_BRIDGE_TOKEN` |
| Install path | Control plane `Hermes:InstallRoot` — must **not** end with `apps/agent-runtime` |

Non-secret Hermes preferences: `~/.hermes/config.yaml`.

---

## Authority checklist

After boot, open Watch or `GET /api/habitat/authority-checklist` and confirm:

- External Hermes InstallRoot (not `apps/agent-runtime`)
- LegacyRuntimeShim inactive
- Habitat observe-only role
