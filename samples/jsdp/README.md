# JSDP harness samples

Example artifacts for the [JSDP Autonomous Convergence Harness](../../docs/jsdp-convergence-harness.md).

| Path | Description |
|------|-------------|
| [PROJECT_SPEC.md](PROJECT_SPEC.md) | Sample JoyMon tile-RPG specification |
| [example-run/](example-run/) | Generated `.jsdp/` state: `config.json`, DAG, prompt, verification report, repair `001R1` |

## Regenerate

```bash
cd samples/jsdp
rm -rf .jsdp example-run
jz jsdp init --spec ./PROJECT_SPEC.md
jz jsdp analyze
jz jsdp plan --mode vertical-slices
jz jsdp next
jz jsdp verify    # fails without JoyMon.sln — expected for sample
jz jsdp continue  # creates repair node 001R1
cp -R .jsdp example-run
```

Repair lineage: node `001` failed verification → `continue` created `001R1` with `repairOf: "001"`.

## External planning (full DAG — use sparingly)

```bash
jz jsdp export-planning-context --mode vertical-slices
jz jsdp planning-prompt --mode vertical-slices
# agent writes plan.json
jz jsdp validate-plan ./plan.json
jz jsdp diff-plan ./plan.json
jz jsdp import-plan ./plan.json --dry-run
jz jsdp import-plan ./plan.json   # add --force if replacing verified DAG
```

## Rolling horizon (recommended for automation)

```bash
jz jsdp horizon export --nodes 3
jz jsdp horizon prompt --nodes 3
# agent writes horizon.json (≤3 nodes)
jz jsdp horizon validate ./horizon.json
jz jsdp horizon diff ./horizon.json
jz jsdp horizon import ./horizon.json --dry-run
jz jsdp horizon import ./horizon.json
jz jsdp horizon status
```

Re-export `horizon export` after `verify` / `continue` changes the DAG. Validate/import refresh the live frontier from `run.json` automatically.

Fixture plans: [valid-plan.json](../../tests/JoyZoning.Cli.Tests/Fixtures/jsdp/valid-plan.json) (passes validation).
