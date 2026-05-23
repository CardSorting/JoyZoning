# Onboarding and Setup Guide

This guide outlines how to prepare, build, and run the integrated JoyZoning and Agent Runtime workspace.

## Prerequisites

Ensure you have the following installed on your system:
- **Node.js** (v18.x or newer, v20+ recommended)
- **pnpm** (v9.x or newer)
- **.NET 8.0 SDK** (required for the C# Control Plane and Desktop Client)
- **Python 3.11** (required for the underlying agent runtime)

---

## One-Touch Setup Playbook

Getting the entire monorepo environment ready is simplified into three primary commands.

### Step 1: Install Dependencies
Run this at the root of the workspace to install all JS/TS dependencies:
```bash
pnpm install
```

### Step 2: Run Setup Wizard
Run the setup wizard to check compatibility, configure environment files, initialize folders, build package projects, and configure the Python virtual environment:
```bash
pnpm setup
```

The script will automatically:
- Verify your Node, pnpm, and .NET SDK versions.
- Ensure ports `9000`, `8642`, `9470`, and `3000` are free.
- Create local `.env` files from templates if they do not exist.
- Initialize the `.joy-workspaces/default/` folders.
- Build the TypeScript contracts and bridges.
- Prepare the Python virtual environment under `apps/agent-runtime/venv` and install its dependencies using `setup-hermes.sh` in non-interactive mode.

### Step 3: Run the Development Stack
To launch all processes concurrently, run:
```bash
pnpm dev
```

This starts:
1. **Control Plane** (port `9470`)
2. **TypeScript Agent Runtime API** (port `9000`)
3. **Next.js Watch UI** (port `3000`)
4. **Desktop App Client** (launches macOS desktop GUI)

---

## Configuration & Secrets

Secrets (e.g., API keys) must be set in `apps/agent-runtime/.env`. 
Open `apps/agent-runtime/.env` and configure key variables such as:
- `OPENAI_API_KEY` (or other provider tokens)
- `HERMES_HOME` (defaults to `~/.hermes`)

Non-secret preferences can be edited in `~/.hermes/config.yaml` or through the interactive setup.
