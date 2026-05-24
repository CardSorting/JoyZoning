# Bounded session + role delivery chain audit

**Governing protocol:** [jsdp.md](jsdp.md) (JoyZoning Sequential Delivery Protocol)

**Date:** May 2026 (pass 2)  
**Decision:** **8 roles, 8 bounded sessions, 1 shared workspace, sequential queue.**  
Multi-agent throughput in one session is rejected. Parallel role sessions on the same chain are rejected.

---

## Model

```text
Delivery chain (one program, one workspace)
  Session 1 (Role 1) ──accept-merge──> Session 2 (Role 2) ──...──> Session 8 (Role 8)
       │                                      │
       └─ 1 task, 1 lease max                └─ seeded from canonical workspace
```

| Layer | Rule |
|-------|------|
| **Chain** | Roles run in sequence 1→8; role N+1 blocked until role N is `Complete` |
| **Session** | `ExecutionMode.BoundedRole`, exactly **1 task**, **1 active lease** |
| **Workspace** | All sessions share physical `WorkspaceRoot`; unique `WorkspaceKey` per session |
| **Consolidation** | Bounded-role sessions are **never merged** by `WorkspaceSessionConsolidator` |

---

## Why not 8 tasks in 1 session?

Even with wave gates, a single session holding 8 tasks invites:
- Operator confusion about which role is canonical
- Stale leases across roles in one merge queue
- Session consolidation collapsing intent

**Fix:** one session per role; chain gate coordinates across sessions.

---

## Enforcement (code)

| Component | Behavior |
|-----------|----------|
| `SessionExecutionMode.BoundedRole` | Isolates session from workspace consolidation |
| `RoleDeliveryChainKeys.WorkspaceKeyForBoundedRole` | Unique DB key per chain step |
| `RoleDeliveryChainGate` | Sequential dispatch across chain |
| `BoundedSessionGate` | Single task + single lease per session |
| `WorktreeSeeder` | Copy canonical workspace into each role's worktree |
| `HandoffPacketBuilder` | Role-scoped paths + bounded prompt |
| `POST /api/delivery-chains` | Create 8 sessions + 8 tasks in one call |
| `GET /api/delivery-chains/{id}/queue` | Next role, blockers, completion counts |

---

## Operator workflow

```bash
# Create 8-role chain (TinyQuest template default)
curl -s -X POST http://127.0.0.1:9470/api/delivery-chains \
  -H 'Content-Type: application/json' \
  -d '{"programName":"TinyQuest Campfire","workspaceRoot":"/Users/bozoegg/Desktop/tinyquest"}' | jq .

# Inspect queue
curl -s http://127.0.0.1:9470/api/delivery-chains/<chainId>/queue | jq .

# Dispatch sequentially (one role at a time)
./scripts/role-chain-dispatch.sh --chain <chainId> --once
# After accept-merge, run again for next role
./scripts/role-chain-dispatch.sh --chain <chainId> --once

# Or create + dispatch in one shot
./scripts/role-chain-dispatch.sh --create --workspace /path/to/tinyquest --program "TinyQuest Campfire" --once
```

---

## Defaults

| Setting | Value |
|---------|-------|
| `MaxActiveLeasesPerSession` | `1` |
| `MaxGlobalActiveLeases` | `4` |
| Roles per chain (template) | `8` |
| Tasks per bounded session | `1` |

---

## Deprecated

| Surface | Replacement |
|---------|-------------|
| 8 tasks in 1 session | `POST /api/delivery-chains` |
| `phased-dispatch.sh` / `stagger-dispatch.sh` | `role-chain-dispatch.sh` |
| Multi-wave parallel gates | `RoleDeliveryChainGate` |

---

## Related

- **[jsdp.md](jsdp.md)** — governing sequential delivery protocol
- [worker-convergence.md](worker-convergence.md)
- [bounded-yolo-coherence-audit.md](bounded-yolo-coherence-audit.md)
