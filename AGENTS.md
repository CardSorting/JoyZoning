# JoyZoning — Agent Entry

**Do not scan the repo.** JoyZoning is self-describing.

**Execution protocol:** All bounded delivery follows **[JSDP](docs/jsdp.md)** — canonical workspace only (no `.joyzoning/worktrees/` or `.joyzoning/live/`), one role per session, sequential chain, mandatory accept-merge between roles. Philosophy: [docs/philosophy.md](docs/philosophy.md).

## Start here

```bash
./scripts/joyzoning agent-context --json
./scripts/joyzoning agent-manifest --json
./scripts/joyzoning doctor --json
```

- **Full docs:** [docs/agent-operations.md](docs/agent-operations.md)
- **Contract:** [docs/AGENT.md](docs/AGENT.md)
- **Sequential delivery:** [docs/jsdp.md](docs/jsdp.md) (JSDP)

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
