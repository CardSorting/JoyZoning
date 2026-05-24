# JoyZoning — Agent Entry

**Do not scan the repo.** JoyZoning is self-describing.

## Start here

```bash
./scripts/joyzoning agent-context --json
./scripts/joyzoning agent-manifest --json
./scripts/joyzoning doctor --json
```

- **Full docs:** [docs/agent-operations.md](docs/agent-operations.md)
- **Contract:** [docs/AGENT.md](docs/AGENT.md)

## Three ways to learn the repo

| Surface | Command / path |
|---------|----------------|
| CLI | `./scripts/joyzoning agent-manifest --json` |
| HTTP | `curl -s http://127.0.0.1:9470/api/agent/context \| jq .` |
| Cache | `.joyzoning/agent-manifest.json` (check `fingerprint`) |

## Rules

1. Read only `importantFiles` from manifest output unless `doctor` fails.
2. Use `endpoints --json` for API discovery — not repo search.
3. After edits: `joyzoning verify --manifest --fast` then `joyzoning snapshot --json`.
