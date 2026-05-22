# Phase 27 — Dogfood validation report

End-to-end validation of the governance boundary (`jz` = human authority, `jz agent` = constrained worker) without new runtime concepts, UI, or orchestration layers.

**Harness:** automated tests in `tests/JoyZoning.Tests/DogfoodValidationTests.cs` plus `scripts/dogfood-validate.sh`.

**Surfaces exercised:**

| Role | Surface |
|------|---------|
| Human cockpit | Desktop (out of scope for CI; API stands in for scheduling) |
| Scheduler | Kanban execution API (dispatch, lease, recovery) |
| Operator shell | `jz` (human merge in happy path) |
| Worker harness | `jz agent` subprocess with worktree `WorkingDirectory` |

---

## 1. Happy path

**Goal:** Low-risk task → dispatch → agent start → heartbeat → verify → agent done → human complete.

| Step | Command / action | Expected | Actual |
|------|------------------|----------|--------|
| Seed + dispatch | `POST api/sessions`, `POST api/tasks`, `POST api/tasks/{id}/dispatch` | Lease `Running`, worktree created | OK |
| Agent bind | `jz agent start --task {id}` (in worktree) | Exit 0, `.joyzoning/context.json` written | OK |
| Heartbeat | `jz agent heartbeat` | Exit 0 | OK (after path fix) |
| Verify | `jz agent verify --cmd true` | Exit 0 | OK |
| Agent done | `jz agent done` | Lease `ReadyForReview`, not `Complete` | OK |
| Human complete | `POST api/tasks/{id}/lease/merge` | Task `Complete` | OK |

**Bugs found:** macOS `/var` vs `/private/var` broke `RequireWorktree`; external `jz` could not reach in-memory TestServer.

**Fixes applied:**

- `JoyZoningRuntimeContext.NormalizeComparablePath` for macOS temp paths.
- Dogfood fixture: subprocess control plane on dynamic port, shared SQLite with test DB; `Testing` stubs via `TestAgentHostSetup`.
- `DogfoodCliRunner`: `--base-url`, `RunInWorktreeAsync` with explicit `WorkingDirectory`.
- `Program.cs`: skip duplicate `ListenUrl` bind when `ASPNETCORE_ENVIRONMENT=Testing`.

---

## 2. Verification failure

**Goal:** Failed verify records evidence; task does not reach `Complete`; `agent done` blocked.

| Step | Expected | Actual |
|------|----------|--------|
| `jz agent verify --cmd false` | Exit 1 | OK |
| `jz agent done` | Exit ≠ 0 | OK |
| Lease status | Not `ReadyForReview` | OK |

No additional code changes beyond path/server fixes above.

---

## 3. Critical path

**Goal:** Critical dispatch requires `--approve-critical`; retry requires fresh approval.

| Step | Expected | Actual |
|------|----------|--------|
| Dispatch without approval | 403 `lease_forbidden` | OK |
| Dispatch with approval | 200 | OK |
| Retry without approval | 409, message mentions approval | OK |
| Retry with approval | 200 | OK |

Validated via in-proc API client (TestServer).

---

## 4. Stale worker path

**Goal:** Stale heartbeat policy blocks lease; evidence preserved.

| Step | Expected | Actual |
|------|----------|--------|
| Seed + dispatch + backdate heartbeat | — | OK |
| `ProcessStaleActiveLeasesAsync` | ≥1 expired | OK |
| Lease status | `Blocked` | OK |
| Evidence | Contains `lease.expired_blocked`, worktree path retained | OK |

---

## 5. Recovery path

**Goal:** Reopen blocked lease preserves evidence lineage.

| Step | Expected | Actual |
|------|----------|--------|
| Simulated dispatch failure | Evidence `dispatch.failed` | OK |
| `Recover` reopen | 200, status `Leased` | OK |
| Evidence | Still contains `dispatch.failed` + `recovery.reopen` | OK |

---

## 6. Guardrail path

**Goal:** In agent mode, forbidden operator commands rejected.

| Command | Expected exit | Actual |
|---------|---------------|--------|
| `jz --agent task complete` | 2 (usage) | OK |
| `jz --agent task revoke` | 2 | OK |
| `jz --agent task dispatch` | 2 | OK |
| `jz --agent raw GET api/health` | 2 | OK |

---

## How to re-run

```bash
./scripts/dogfood-validate.sh
```

Or:

```bash
dotnet test tests/JoyZoning.Tests/JoyZoning.Tests.csproj --filter "FullyQualifiedName~Dogfood"
```

---

## Remaining sharp edges

1. **Dogfood server is a subprocess**, not the desktop app — manual desktop/kanban UI flows are documented in `scripts/examples/*.sh` but not CI-gated.
2. **SQLite sharing** between TestServer and dogfood subprocess relies on one file path; avoid parallel dogfood collections on the same port.
3. **`dotnet run` per jz invocation** is slow (~4s per test); acceptable for validation, not for tight inner loops.
4. **Testing stubs** only apply when `ASPNETCORE_ENVIRONMENT=Testing`; production dogfood must use real Hermes or operator-approved mocks.
5. **Human merge in happy path** uses API `lease/merge`, not `jz task complete` — intentional (human authority stays on operator surface).

---

## Summary

| Path | Result |
|------|--------|
| 1 Happy | Pass |
| 2 Verify fail | Pass |
| 3 Critical | Pass |
| 4 Stale | Pass |
| 5 Recovery | Pass |
| 6 Guardrails | Pass |

**Governance invariant confirmed:** `agent done` → `ReadyForReview`; human merge → `Complete`. Agents cannot launder completion into project truth.
