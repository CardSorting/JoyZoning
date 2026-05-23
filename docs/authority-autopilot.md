# Authority autopilot (bounded YOLO)

JoyZoning can auto-accept verified workers into the **canonical session workspace** (real git convergence) when an **authority profile** says it is safe. Autopilot does **not** mean unlimited autonomy: it means policy supervises convergence and humans only review exceptions.

## Mental model

```text
Worker ReadyForReview + verification passed
  → merge observability (overlaps, conflicts, changed files)
  → authority policy evaluate
  → auto-accept (git merge) OR needs human review
```

Humans should wake up when the goblin touched payments—not for every docs typo.

## Profiles

Configure under `Authority` in `appsettings.json` (or environment-specific overrides).

| Profile | Behavior |
|---------|----------|
| **Conservative** | Never auto-accept. Every ReadyForReview worker needs human **Accept result**. |
| **Balanced-Auto** (default) | Auto-accept low/medium-risk, verified changes within limits. Blocks protected paths, conflicts, overlaps, failed verification, large change sets, high/critical task risk. |
| **YOLO** | Broader auto-accept (still blocks protected paths, secrets, deps, auth, payments, infra, migrations, CI, destructive/git failures). Larger file-count ceiling (50). |
| **Custom** | Operator-defined `DenyPathGlobs`, `AllowPathGlobs`, `MaxChangedFiles`, and `AllowedRiskLevels`. |

### Session overrides

Per-session profile via `Authority:SessionProfileOverrides`:

```json
{
  "Authority": {
    "AutopilotEnabled": true,
    "DefaultProfile": "BalancedAuto",
    "SessionProfileOverrides": {
      "my-dev-session": "Yolo",
      "3fa85f64-5717-4562-b3fc-2c963f66afa6": "Conservative"
    }
  }
}
```

Keys may be **session name** or **session id** (guid string).

### Disable autopilot

```json
{
  "Authority": {
    "AutopilotEnabled": false
  }
}
```

Behaves like Conservative for automatic accepts (manual accept still works).

## What auto-accept requires

All must be true:

- Lease status `ReadyForReview`
- Passing verification report attached
- `LeaseRuntime:MetadataOnlyAcceptResult` is `false` (real git convergence enabled)
- Merge observability: `ready_to_merge` (no merge conflict / failed preconditions)
- **No overlapping** changed files with another ReadyForReview worker in the same session
- Changed files known (not empty when multiple ready workers exist)
- No protected-path hits (see below)
- Profile-specific risk/file limits satisfied
- Git convergence succeeds when accept runs (failure leaves lease ReadyForReview and records `authority.auto_blocked`)

## Protected paths and categories

`ProtectedPathMatcher` blocks auto-accept when changed files match categories including:

- Authentication / authorization (`auth`, `login`, `oauth`, …)
- Payments / checkout / billing
- Admin authorization surfaces
- Database migrations and schema
- Secrets, env, credentials, config
- Infrastructure and deployment manifests
- Package and dependency manifests (`package.json`, `*.csproj`, lockfiles, …)
- CI/CD workflows
- Security policy files

YOLO and Balanced-Auto both enforce these; YOLO only relaxes **risk and file-count** thresholds—not safety categories.

## Merge observability (pre-autopilot)

Before policy runs, `AuthorityAutopilotMergeContextBuilder`:

1. Lists all **ReadyForReview** leases in the session
2. Resolves changed paths (verification report, else git worktree summary)
3. Builds a ready-worker file map and detects **overlapping_files**
4. Enriches merge state via `WorkerMergeObservabilityBuilder`

Autopilot blocks when:

- Another ready worker touched the same path (`overlapping_ready_worker`)
- Merge state is `merge_conflict` / `merge_failed`
- Conflict risk is **unknown** (e.g. multiple ready workers but changed files could not be resolved)

Blocked workers get `authority.auto_blocked` evidence with `overlappingPaths`, `reasonCodes`, and human-readable messages.

## Human review

The Watch operator console shows:

- **Autopilot · bounded YOLO** bar (active profile)
- **Autopilot activity** (auto-accepted vs blocked)
- **Needs review** queue (policy-blocked + conflicts only)

Manual **Accept result** remains available for blocked workers after review.

## Evidence events

Written to the lease `EvidenceLogJson`:

| Kind | When |
|------|------|
| `authority.decision` | Every autopilot evaluation (allowed or blocked) |
| `authority.auto_blocked` | Policy denied auto-accept (includes overlap/protected path reasons) |
| `authority.auto_accepted` | Git convergence succeeded via autopilot |
| `git.convergence.succeeded` / `git.convergence.failed` | Real merge outcome (see `docs/real-git-convergence.md`) |

Decision detail includes: `profile`, `riskLevel`, `autoAcceptAllowed`, `needsHumanReview`, `reasonCodes`, `humanMessages`, `changedFiles`, `protectedPaths`.

Blocked detail adds: `mergeState`, `mergeObservabilityUnknown`, `overlappingPaths`.

## Reconciliation

Stale ReadyForReview workers (created before autopilot, or missed on verification) are re-evaluated on the **lease reconciliation** timer (`LeaseRuntime:ReconciliationIntervalSeconds`, default 30s) when `Authority:ReconcileReadyForReview` is true.

Manual triggers:

- `POST /api/authority/reconcile-ready` — all sessions
- `POST /api/sessions/{sessionId}/authority/reconcile` — one session
- Watch operator console: **Reconcile pending** on the autopilot bar (same session endpoint)

Reconciliation re-runs merge observability + policy and may auto-accept eligible workers. Evidence is **deduplicated**: identical policy outcomes do not append new `authority.decision` / `authority.auto_blocked` entries every tick.

## Configuration reference

```json
{
  "Authority": {
    "AutopilotEnabled": true,
    "ReconcileReadyForReview": true,
    "DefaultProfile": "BalancedAuto",
    "SessionProfileOverrides": {},
    "Custom": {
      "MaxChangedFiles": 25,
      "DenyPathGlobs": [],
      "AllowPathGlobs": [],
      "AllowedRiskLevels": ["Low", "Medium"]
    }
  },
  "LeaseRuntime": {
    "MetadataOnlyAcceptResult": false
  },
  "WorkspaceParallelism": {
    "LargeChangeSetFileThreshold": 20
  }
}
```

## Related docs

- [Worker convergence](worker-convergence.md) — worktrees and operator flow
- [Real git convergence](real-git-convergence.md) — Accept result applies code to main before `Merged`
