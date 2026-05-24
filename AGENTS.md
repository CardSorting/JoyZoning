# JoyZoning — Agent Entry

**Do not scan the repo.** JoyZoning is self-describing.

## Start here

```bash
./scripts/joyzoning agent-context --json
./scripts/joyzoning agent-manifest --json   # also writes .joyzoning/agent-manifest.json
./scripts/joyzoning doctor --json
```

Read [docs/AGENT.md](docs/AGENT.md) for the full contract.

## Offline cache

After `agent-manifest` or a passing local `doctor`, read:

`.joyzoning/agent-manifest.json`

Check `fingerprint` matches current — if stale, re-run `agent-manifest`.

## HTTP fallback (control plane running)

```bash
curl -s http://127.0.0.1:9470/api/agent/context | jq .
curl -s http://127.0.0.1:9470/api/agent/manifest | jq .
curl -s 'http://127.0.0.1:9470/api/agent/endpoints?agentSafe=true' | jq .
```

## Rules

1. Read only `importantFiles` from manifest output unless `doctor` reports stale assumptions.
2. Use `endpoints --json` or `/api/agent/endpoints` for API discovery — not repo search.
3. After edits: `joyzoning verify --manifest --fast` then `joyzoning snapshot --json`.
