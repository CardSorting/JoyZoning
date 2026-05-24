# JoyZoning Agent Contract

JoyZoning is **self-describing software**. Agents should ask the CLI what it is — not archaeology the repo.

## Canonical interface

Use `joyzoning` (or `jz`) as the machine control surface:

```bash
joyzoning agent-context --json   # state before acting
joyzoning agent-manifest --json  # commands, endpoints, verification, protected paths
joyzoning endpoints --json         # typed API registry
joyzoning doctor --json            # validate assumptions; scan repo only if this fails
```

## Before editing

1. Run `joyzoning agent-context --json`
2. Run `joyzoning status --json`
3. Read only files listed in `importantFiles` from the manifest or inspect output

Do **not** scan the whole repo unless `joyzoning doctor --json` reports stale or missing assumptions.

## After editing

1. Run `joyzoning verify` (or manifest `verification` commands)
2. Run `joyzoning snapshot --json`
3. Report changed files, verification result, and remaining risks

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
