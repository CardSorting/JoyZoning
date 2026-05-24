# JoyZoning — Agent Entry

**Do not scan the repo.** JoyZoning is self-describing.

**Execution protocol:** All bounded delivery follows **[JSDP](docs/jsdp.md)** — canonical workspace only (no `.joyzoning/worktrees/` or `.joyzoning/live/`), one role per session, sequential chain, mandatory accept-merge between roles. **Hermes is optional:** external agents (Cursor, Claude Code, manual) use [external-agent JSDP](docs/external-agent-jsdp.md) — JoyZoning owns state; tools only edit files. Philosophy: [docs/philosophy.md](docs/philosophy.md).

## Start here

```bash
./scripts/joyzoning agent-context --json
./scripts/joyzoning agent-manifest --json
./scripts/joyzoning doctor --json
```

**External JSDP (you edit files; JoyZoning owns state):**

```bash
jz delivery-chain next <chain-id> --external --agent cursor
jz task prompt <task-id>    # read handoff — do not mark Complete via API
jz task mark-ready <task-id>   # operator only
```

- **Full docs:** [docs/agent-operations.md](docs/agent-operations.md)
- **Contract:** [docs/AGENT.md](docs/AGENT.md)
- **Sequential delivery:** [docs/jsdp.md](docs/jsdp.md) (JSDP)
- **Execution paths:** [docs/execution-paths.md](docs/execution-paths.md) (managed vs external)
- **External path:** [docs/external-agent-jsdp.md](docs/external-agent-jsdp.md)
- **Framework (optional):** [docs/whitepaper-summary.md](docs/whitepaper-summary.md) — C1–C7; product is Annex A embodiment only

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
