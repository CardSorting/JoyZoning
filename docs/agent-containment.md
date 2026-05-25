# Agent Containment & Safety

To prevent the agent runtime from performing unsafe operations or corrupting the parent JoyZoning application codebase, the system implements a strict containment boundary.

## Containment Metaphor: The Fenced Yard

All agent operations are restricted to a "Fenced Yard" defined by the workspace root:

```
JOY_WORKSPACE_ROOT = /Users/bozoegg/Desktop/JoyZoning/.joy-workspaces/default
```

### Safety Rules

1. **Strict Path Restriction**: Every file read and write operation executed by the agent must resolve to a path inside the `JOY_WORKSPACE_ROOT` (or `HERMES_WRITE_SAFE_ROOT` environment variable).
2. **Traversal Protection**: Relative paths or directory traversal shortcuts (e.g. `../../`) are resolved to absolute realpaths and checked. Attempts to target folders outside the root are blocked.
3. **App Source Code Protection**: Writing to the main JoyZoning codebase (under `/apps/`, `/src/`, `/packages/`, etc.) is strictly forbidden by default.
4. **Approval Loop**: If the agent attempts a write to the JoyZoning app source files, the file system boundary intercepts it. Unless YOLO mode is active (`HERMES_YOLO_MODE=1`), it forces a CLI prompt asking the operator for explicit permission to proceed. If denied, the write is aborted and logged.

---

## Log Locations

When the agent attempts to perform a file operation that violates containment:
- The system increments the violation counter inside:
  `[WorkspaceRoot]/agent-state/containment-status.json`
- The system logs the exact date, time, and blocked file path to:
  `[WorkspaceRoot]/logs/containment.log`

These files are polled by the JoyZoning operator dashboard to dynamically display containment health status.

---

## Technical Details

- **TypeScript Side**: The `WorkspaceModel` in `packages/workspace-core` implements `validatePath(targetPath)` to check if a path lies inside the workspace boundary.
- **Hermes runtime (external install)**: `tools/file_tools.py` and path security helpers in your `Hermes:InstallRoot` checkout enforce read/write boundaries before disk I/O. JoyZoning no longer vendors Hermes under `apps/agent-runtime/`.
- **Workspace-core**: still used for JoyZoning-side path validation when legacy scripts reference `WorkspaceModel`; canonical containment for agent work is Hermes + `JOY_WORKSPACE_ROOT`.
