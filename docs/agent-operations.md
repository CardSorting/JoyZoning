# Agent Operations Layer

JoyZoning exposes a **stable machine interface** so coding agents do not need to spelunk `src/`, route files, package scripts, or logs to understand how to operate the repo.

**Manifest version:** `1` (`manifestVersion` in JSON)

**Agent contract (short):** [AGENT.md](AGENT.md)  
**Cursor entry:** [../AGENTS.md](../AGENTS.md)  
**CLI reference:** [cli.md](cli.md#agent-operations-layer)

---

## Problem this solves

Without an agent operations layer, every new agent session repeats the same archaeology:

- Where are API routes defined?
- What verification commands exist?
- Which files are safe to edit?
- What is the current session / task / approval state?

JoyZoning answers those questions **about itself** through CLI commands, HTTP endpoints, and a cached manifest file.

---

## Architecture

```text
┌─────────────────────────────────────────────────────────────┐
│  Domain (single source of truth)                            │
│  AgentOperationsManifest.cs      static commands, workflow  │
│  AgentOperationsManifestCache.cs fingerprint + file cache   │
│  JoyZoningEndpointRegistry.cs    typed API routes           │
│  JoyZoningEndpointRegistrySync.cs  drift detection          │
└───────────────────────────┬─────────────────────────────────┘
                            │
         ┌──────────────────┼──────────────────┐
         ▼                  ▼                  ▼
   joyzoning / jz     Control plane HTTP   .joyzoning/
   (CLI)              :9470                 agent-manifest.json
```

| Surface | When to use |
|---------|-------------|
| **CLI** `./scripts/joyzoning` | Full context: git state, canonical workspace root, card branch, verification |
| **HTTP** `/api/agent/*` | CLI not installed; control plane running |
| **Cache** `.joyzoning/agent-manifest.json` | Offline; fingerprint still valid |
| **Watch bootstrap** `/api/watch/bootstrap` → `agentOps` | UI-embedded agent hints |

Registry sync (registry ↔ live routes) is enforced by CI tests and `doctor --json` (`endpoint_registry_sync`).

---

## Quick start (agents)

```bash
./scripts/joyzoning agent-context --json
./scripts/joyzoning agent-manifest --json   # writes cache
./scripts/joyzoning doctor --json
```

**Do not scan the repo** unless `doctor` reports stale or missing assumptions.

---

## CLI commands

All agent-ops commands emit **JSON on stdout** by default. Use `--pretty` for human-readable output.

Install: `./scripts/install-jz.sh` (provides `jz` and `joyzoning` in `~/.local/bin`)  
Dev wrapper: `./scripts/joyzoning` (uses `dist/jz-publish` when present)

### Discovery

| Command | Purpose |
|---------|---------|
| `agent-context --json` | Minimal state before acting: session, git, health, tasks, approvals |
| `agent-manifest --json` | Full manifest; writes `.joyzoning/agent-manifest.json` |
| `inspect --json` | Compressed discovery: important files, protected paths, entrypoints |
| `endpoints --json` | Typed endpoint registry (all routes) |
| `endpoints --agent-safe` | Agent-safe routes only (~41 of ~63) |
| `endpoints --markdown` | Markdown table for docs |

### Health and verification

| Command | Purpose |
|---------|---------|
| `doctor --json` | Validate assumptions; refreshes cache on local pass |
| `status --json` | Workspace + control-plane summary |
| `snapshot --json` | Git, tests, sessions, approvals after edits |
| `verify --manifest --fast` | typecheck + build only |
| `verify --manifest` | Full tier (includes tests + watch UI checks) |

### Task orchestration

| Command | Purpose |
|---------|---------|
| `plan "<goal>" --session <guid>` | Create a task from a goal string |
| `run <task-id>` | Dispatch and run a task lease (managed) |
| `task start-external <id> --agent cursor` | External JSDP — no lease ([external-agent-jsdp.md](external-agent-jsdp.md)) |
| `task prompt / status / mark-ready / complete` | External workflow gates |
| `delivery-chain create \| queue \| next --external` | Sequential JSDP chain |
| `task list / read / verify` | Full task workflow (see [cli.md](cli.md)) |

---

## HTTP endpoints

Base URL: `http://127.0.0.1:9470` (`JOYZONING_URL`)

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/api/agent/manifest` | Static manifest (same core as CLI manifest) |
| `GET` | `/api/agent/context` | Runtime state: sessions, leases, pending approvals |
| `GET` | `/api/agent/endpoints` | Endpoint registry; `?agentSafe=true` filters |
| `GET` | `/api/watch/bootstrap` | Watch UI bootstrap; includes `agentOps` block |

```bash
curl -s http://127.0.0.1:9470/api/agent/context | jq .
curl -s http://127.0.0.1:9470/api/agent/manifest | jq .
curl -s 'http://127.0.0.1:9470/api/agent/endpoints?agentSafe=true' | jq .
```

See also [control-plane-api.md](control-plane-api.md#agent-operations).

---

## Offline cache

Path: `.joyzoning/agent-manifest.json` (gitignored)

Written when:

- `joyzoning agent-manifest --json` runs
- `joyzoning doctor --json` passes **local** checks

Structure:

```json
{
  "fingerprint": "1|endpoints=63|files=12|...|sync=ok|api=63",
  "importantFilesHash": "abc123...",
  "generatedAt": "2026-05-23T...",
  "manifest": { ... }
}
```

Re-run `agent-manifest` when `doctor` reports `manifest_cache: warn`.

---

## Agent workflow

### Before editing

1. `joyzoning agent-context --json`
2. `joyzoning status --json`
3. Read only paths in `importantFiles` from manifest or inspect output

### After editing

1. `joyzoning verify --manifest --fast`
2. `joyzoning snapshot --json`
3. Report changed files, verification result, remaining risks

### Task-bound work (canonical workspace)

Use `joyzoning verify --cmd "..."` or `joyzoning task verify <id> --cmd "..."`.  
Agents stop at **ReadyForReview**; humans own merge and Complete.

---

## Manifest fields (key)

| Field | Meaning |
|-------|---------|
| `manifestVersion` | Schema version (`1`) |
| `fingerprint` | Staleness detector (endpoints, files, sync) |
| `importantFiles` | Files agents may read without full repo scan |
| `protectedPaths` | Paths agents must not edit |
| `verification` | Map of named verification commands |
| `verificationTiers.fast` | Subset for `--fast` verify |
| `endpoints` / `agentSafeEndpoints` | Typed API registry |
| `endpointSummary.syncedWithApi` | Registry matches `ApiEndpoints.cs` |
| `workflow` | Named workflow strings (`beforeEdit`, `afterEdit`, …) |
| `http` | Relative HTTP paths for agent surfaces |

---

## Doctor checks (agent-related)

Local checks (fail closed on `fail`, warn on `warn`):

| Check ID | Meaning |
|----------|---------|
| `endpoint_registry_sync` | Registry matches all routes in `ApiEndpoints.cs` |
| `agent_http_manifest` | Registry documents `GET /api/agent/manifest` |
| `agent_http_context` | Registry documents `GET /api/agent/context` |
| `agents_entry` | Root `AGENTS.md` exists |
| `manifest_fresh` | `docs/AGENT.md` references canonical commands |
| `manifest_cache` | `.joyzoning/agent-manifest.json` fingerprint current |
| `cli_publish` | `dist/jz-publish/jz` not older than CLI sources |
| `required_files` | All `importantFiles` exist |

If `endpoint_registry_sync: fail`, do **not** trust `endpoints --json` until the registry is repaired.

---

## Protected paths

Never edit (from manifest `protectedPaths` / inspect `doNotEdit`):

- `.next/`, `node_modules/`, `generated/`, `dist/`, `bin/`, `obj/`

---

## Source files (maintainers)

| File | Role |
|------|------|
| `src/JoyZoning.Domain/Orchestration/AgentOperationsManifest.cs` | Static manifest data |
| `src/JoyZoning.Domain/Orchestration/AgentOperationsManifestCache.cs` | Cache + fingerprint |
| `src/JoyZoning.Domain/Orchestration/JoyZoningEndpointRegistry.cs` | Typed endpoint list |
| `src/JoyZoning.Domain/Orchestration/JoyZoningEndpointRegistrySync.cs` | Drift detection |
| `src/JoyZoning.Cli/AgentOperationsCommand.cs` | CLI command implementation |
| `src/JoyZoning.ControlPlane/Endpoints/ApiEndpoints.cs` | Live HTTP routes + `/api/agent/*` |
| `scripts/joyzoning` | Canonical dev wrapper |
| `scripts/jz` | CLI wrapper (prefers `dist/jz-publish`) |

When adding API routes, see [development.md](development.md#adding-api-endpoints).

---

## Tests

| Test project | Coverage |
|--------------|----------|
| `tests/JoyZoning.Cli.Tests/AgentOperationsCommandTests.cs` | CLI JSON output, cache, doctor |
| `tests/JoyZoning.Cli.Tests/JoyZoningEndpointRegistrySyncTests.cs` | Registry ↔ source file |
| `tests/JoyZoning.Tests/AgentOperationsHttpTests.cs` | HTTP manifest, context, bootstrap |
| `tests/JoyZoning.Tests/JoyZoningLiveEndpointRegistrySyncTests.cs` | Registry ↔ live routes |

Run: `./scripts/run-tests.sh fast` (includes CLI unit tests)

---

## Verification tiers

| Tier | Command | Commands run |
|------|---------|--------------|
| Fast | `verify --manifest --fast` | `typecheck`, `build` |
| Full | `verify --manifest` | + `tests`, `watchTypecheck`, `watchTests` |

Commands are defined in `AgentOperationsManifest.VerificationCommands`.
