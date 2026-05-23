# JoyZoning & Diet-Hermes Fusion Architecture

This document describes the design, system boundaries, and structural metaphors of the fused JoyZoning monorepo workspace.

## Structural Metaphor

To understand how the elements of the codebase relate and interoperate, we use the following mental model:

```
+-------------------------------------------------------------+
| JoyZoning (House / Operator Shell)                          |
|                                                             |
|   +------------------+                                      |
|   |   Agent Bridge   | <=== (Front Door: HTTP / Events)     |
|   +--------+---------+                                      |
|            |                                                |
+------------|------------------------------------------------+
             |
             v
+------------|------------------------------------------------+
| .joy-workspaces/default/ (Fenced Yard / Workspace Root)     |
|                                                             |
|   +--------+---------+                                      |
|   |  Agent Runtime   | (Manager / Hermes Gateway)           |
|   |  Proxy Server    |                                      |
|   +--------+---------+                                      |
|            |                                                |
|            v                                                |
|   +--------+---------+                                      |
|   |    Diet-Hermes   | (Worker / Python Agent Process)      |
|   |  Python Engine   |                                      |
|   +------------------+                                      |
|                                                             |
|   [Containment Walls] (Blocks reads/writes outside Yard)    |
|   [Locking Mechanism] (Prompts for app source write approval) |
+-------------------------------------------------------------+
```

* **JoyZoning (The House)**: The operator shell, primary UI, and onboarding system. It sits at the top level and serves as the control room or control plane.
* **Hermes (The Manager)**: The typescript proxy API server in `apps/agent-runtime` that acts as the manager. It coordinates task startup, tracks agent state, exposes endpoints, and spawns the worker.
* **DietCode (The Worker)**: The Python execution layer (`diet-hermes` gateway runtime) performing file edits and shell commands.
* **Workspace (The Fenced Yard)**: The restricted directory `JOY_WORKSPACE_ROOT` (defaulting to `.joy-workspaces/default/`) inside which all agent work must occur.
* **Bridge (The Front Door)**: The typed client library `packages/agent-bridge` and contracts `packages/shared-contracts` providing the only legal way for the House to communicate with the Manager/Worker.
* **Containment (Walls & Locks)**: Path validation logic in `file_safety.py` that blocks read/write operations attempting to escape the fenced yard, and prompts for explicit human approval before modifying the house's structure (JoyZoning code files).

---

## Workspace Structure

The monorepo is organized as a clean `pnpm` workspace to maintain absolute isolation:

```
JoyZoning/
  ├── apps/
  │   ├── joyzoning/         # Next.js visual cockpit UI
  │   └── agent-runtime/     # Python diet-hermes engine & TS proxy server
  ├── packages/
  │   ├── shared-contracts/  # TypeScript interfaces & event schemas
  │   ├── workspace-core/    # Directory mapping & path validation utilities
  │   └── agent-bridge/      # Typed client used by apps/joyzoning
  ├── scripts/
  │   ├── setup.ts           # One-touch setup wizard
  │   └── dev-all.ts         # Multi-process development runner
  ├── .joy-workspaces/       # Workspace root containing actual repositories & logs
  └── package.json           # Monorepo task definitions
```

---

## Inter-App Communication Rules

1. **No Direct Imports**: Under no circumstances should `apps/joyzoning` perform direct JS/TS imports from `apps/agent-runtime` or vice versa.
2. **Explicit Interfaces**: All communication must go through `packages/agent-bridge` over the local HTTP server (port `9000`).
3. **Contracts Alignment**: Event schemas and state shapes are strictly governed by `packages/shared-contracts`. Any protocol or data format changes must be updated there first.
