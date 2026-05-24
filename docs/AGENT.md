# JoyZoning Agent Contract

JoyZoning is **self-describing software**. Agents should ask the CLI what it is — not archaeology the repo.

**Manifest version:** `1` (field `manifestVersion` in JSON output)

## Canonical interface

Use `joyzoning` (or `jz`) as the machine control surface:

```bash
joyzoning agent-context --json   # state before acting
joyzoning agent-manifest --json  # commands, endpoints, verification, protected paths
joyzoning endpoints --json         # typed API registry
joyzoning endpoints --agent-safe   # agent-safe routes only
joyzoning doctor --json            # validate assumptions; scan repo only if this fails
```

### HTTP fallback (control plane running)

When the CLI is unavailable but the control plane is up:

```bash
curl -s http://127.0.0.1:9470/api/agent/manifest | jq .
curl -s 'http://127.0.0.1:9470/api/agent/endpoints?agentSafe=true' | jq .
```

If `doctor` reports `endpoint_registry_sync: fail`, the typed registry drifted from live API routes — fix the registry before trusting `endpoints --json`.

## Before editing

1. Run `joyzoning agent-context --json`
2. Run `joyzoning status --json`
3. Read only files listed in `importantFiles` from the manifest or inspect output

Do **not** scan the whole repo unless `joyzoning doctor --json` reports stale or missing assumptions.

Root entry for Cursor/agents: [AGENTS.md](../AGENTS.md)

## After editing

1. Run `joyzoning verify --manifest --fast` (typecheck + build; use full `--manifest` before merge)
2. Run `joyzoning snapshot --json`
3. Report changed files, verification result, and remaining risks

For task-bound work inside a lease worktree, use `joyzoning verify --cmd "..."` or `joyzoning task verify <id> --cmd "..."`.

## Task workflow

```bash
joyzoning plan "fix broken verification panel" --session <guid>
joyzoning task list --session <guid>
joyzoning task read <id>
joyzoning run <id>
joyzoning task verify <id> --cmd "dotnet build JoyZoning.sln"
```

Agents stop at **ReadyForReview**. Humans own merge and Complete.

## Protected paths

Never edit paths listed in `protectedPaths` / `doNotEdit` from inspect output (e.g. `.next/`, `node_modules/`, `generated/`, `dist/`, `bin/`, `obj/`).

## Endpoint registry

- Source of truth for agent-safe API discovery: `src/JoyZoning.Domain/Orchestration/JoyZoningEndpointRegistry.cs`
- Static manifest source: `src/JoyZoning.Domain/Orchestration/AgentOperationsManifest.cs`
- Must stay in sync with `src/JoyZoning.ControlPlane/Endpoints/ApiEndpoints.cs`
- `doctor --json` runs `endpoint_registry_sync`; CI tests enforce parity

## Verification tiers

| Tier | Command | Runs |
|------|---------|------|
| Fast | `verify --manifest --fast` | typecheck, build |
| Full | `verify --manifest` | typecheck, tests, build, watch UI checks |
