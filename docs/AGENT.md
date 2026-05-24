# JoyZoning Agent Contract

JoyZoning is **self-describing software**. Ask the CLI or HTTP API what it is — do not archaeology the repo.

**Full reference:** [agent-operations.md](agent-operations.md)  
**Sequential delivery protocol:** [jsdp.md](jsdp.md) (JSDP — required for bounded role chains)  
**Manifest version:** `1` (JSON field `manifestVersion`)

## Start here

```bash
./scripts/joyzoning agent-context --json
./scripts/joyzoning agent-manifest --json
./scripts/joyzoning doctor --json
```

Or read the offline cache: `.joyzoning/agent-manifest.json`  
Or HTTP (control plane running): `GET /api/agent/context`

Cursor entry: [../AGENTS.md](../AGENTS.md)

## Before editing

1. Run `joyzoning agent-context --json` and `joyzoning status --json`
2. Read only `importantFiles` from the manifest
3. Do **not** scan the whole repo unless `doctor --json` fails

## After editing

1. `joyzoning verify --manifest --fast` (full `--manifest` before merge)
2. `joyzoning snapshot --json`
3. Report changed files, verification result, and remaining risks

## Rules

- API discovery: `endpoints --json` or `/api/agent/endpoints?agentSafe=true` — not repo search
- If `endpoint_registry_sync: fail` in doctor, fix the registry before trusting endpoints
- If `manifest_cache: warn`, re-run `agent-manifest`
- Never edit `protectedPaths` (`.next/`, `node_modules/`, `generated/`, etc.)
- Agents stop at **ReadyForReview**; humans merge and Complete
- Bounded multi-role delivery: follow **[JSDP](jsdp.md)** — `POST /api/delivery-chains`, `./scripts/role-chain-dispatch.sh`

## Task workflow

```bash
joyzoning plan "fix broken verification panel" --session <guid>
joyzoning task list --session <guid>
joyzoning run <id>
joyzoning task verify <id> --cmd "dotnet build JoyZoning.sln"
```
