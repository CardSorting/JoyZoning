# JSDP Autonomous Convergence Harness

The **JSDP Autonomous Convergence Harness** is a local, deterministic orchestration runtime for long-horizon software mutation. It turns a project specification into a **dependency-aware prompt DAG**, executes one node at a time, verifies convergence, and records append-only operational history.

This is **not** a generic agent framework or an infinite autonomous loop. JoyZoning remains the operator habitat; this harness is a **project-aware compiler for staged agent execution**.

For the existing 8-role delivery protocol (managed Hermes or external Cursor), see [jsdp.md](jsdp.md) and [external-agent-jsdp.md](external-agent-jsdp.md).

**Boundary:** JoyZoning does not embed an LLM planner. External agents may author `plan.json`; the harness validates, imports, and owns checkpoints.

---

## What JSDP means here

| Concept | Meaning |
|---------|---------|
| **JoyZoning** | Operator habitat — sessions, branches, merge gates |
| **JSDP (delivery)** | 8-role sequential delivery chain (`jz delivery-chain`) |
| **JSDP (harness)** | Project-specific prompt DAG in `.jsdp/` (`jz jsdp`) |

The harness maximizes **reviewability**, **recoverability**, and **deterministic convergence** — not raw throughput.

---

## Storage layout

All state is human-readable under `.jsdp/`:

```text
.jsdp/
  run.json              # Active run + DAG nodes
  project-spec.json     # Goal + spec + analysis
  tree.json             # DAG summary + convergence health
  ledger.jsonl          # Append-only operational history
  prompts/              # Generated per-node prompts
  reports/              # Verification reports
  state/                # Reserved for checkpoints
```

---

## CLI workflow

```bash
joyzoning jsdp init --spec ./PROJECT_SPEC.md
joyzoning jsdp analyze
joyzoning jsdp plan --mode vertical-slices
joyzoning jsdp next
# ... agent executes prompt in .jsdp/prompts/<id>.md ...
joyzoning jsdp verify
joyzoning jsdp continue
joyzoning jsdp status

joyzoning jsdp record --node 001 --summary "Implemented tilemap collision"
```

### Commands

| Command | Purpose |
|---------|---------|
| `init "<goal>"` | Create `.jsdp/` and empty DAG |
| `init --spec <file>` | Load markdown project spec |
| `analyze` | Extract structured metadata → `project-spec.json` |
| `plan --mode <mode>` | Generate project-specific DAG |
| `next` | Select next dependency-ready node; write prompt |
| `verify` | Run node verification commands; write report |
| `continue` | Advance after pass, or create repair node on failure |
| `status` | Convergence health and node states |
| `record` | Append operator ledger entry |
| `inspect` | Spec analysis, DAG, current node, last ledger entry, repair lineage |
| `doctor` | Missing files, malformed DAG, blocked deps, missing verification, stale currentNodeId |
| `export-planning-context` | Write `.jsdp/state/planning-context.json` for external planners |
| `planning-prompt` | Generate external-agent prompt + schema paths |
| `validate-plan <file>` | Validate `plan.json` without writing |
| `import-plan <file>` | Import validated plan → `run.json` + `tree.json` |

### External agent planning (no internal LLM)

```bash
jz jsdp init --spec ./PROJECT_SPEC.md
jz jsdp analyze
jz jsdp export-planning-context --mode vertical-slices
jz jsdp planning-prompt --mode vertical-slices
# external agent writes plan.json using context + schema
jz jsdp validate-plan ./plan.json
jz jsdp import-plan ./plan.json
jz jsdp next
```

The external agent creates the map. JSDP controls checkpoints.

### Config (`.jsdp/config.json`)

Verification presets for `plan`. Created on `init` from repo scan; updated on `analyze` when the spec declares verification commands.

```json
{
  "defaultVerificationPreset": "fast",
  "verificationPresets": {
    "fast": ["dotnet build My.sln", "dotnet test My.Tests/My.Tests.csproj"],
    "full": ["dotnet build My.sln", "dotnet test My.Tests/My.Tests.csproj", "./scripts/run-tests.sh fast"]
  },
  "repoScan": { "enabled": true, "maxDepth": 4 }
}
```

Sample: [samples/jsdp/example-run/config.json](../samples/jsdp/example-run/config.json).

---

## Planning modes

| Mode | Best for | Shape |
|------|----------|-------|
| `vertical-slices` | Games, demos, UX-first apps | End-to-end usable slices |
| `systems-first` | Platforms, infra, enterprise | Domain → events → persistence → runtime |
| `risk-first` | Research, unknown feasibility | Spikes and safety proofs first |

---

## Repair nodes

When `verify` fails, `continue` creates a repair node (e.g. `007R1`) that:

- Preserves lineage via `repairOf`
- Reuses verification commands and mutation surface
- Blocks the failed node until repair converges

---

## Verification flow

1. Each node declares `verificationCommands` (tests, build, typecheck, etc.).
2. `jsdp verify` runs them in the workspace root — no silent skips.
3. Results land in `.jsdp/reports/<node-id>-verification.md`.
4. Node status becomes `verified` or `failed`.
5. A ledger entry is **appended** (never overwritten).

---

## Ledger semantics

`ledger.jsonl` is **append-only**. Each line is one `LedgerEntry` with:

- timestamp, nodeId, summary, filesChanged
- verification commands, pass/fail, failures
- optional next recommendation

---

## Resumability

After interruption:

1. Inspect `.jsdp/run.json` and `status`
2. Re-run `next` for the current or next ready node
3. Ledger and reports preserve full history

---

## Convergence rules

- Never silently skip verification
- Never continue after failed convergence without repair
- Never overwrite ledger history
- Never mutate outside declared surfaces (enforced by prompt contract; operator reviews diffs)

---

## Samples

See [samples/jsdp/](../samples/jsdp/) for example spec, DAG, prompts, reports, and repair flow.
