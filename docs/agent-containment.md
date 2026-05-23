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
- **Python Side**: `apps/agent-runtime/agent/file_safety.py` defines `is_write_denied(path)`. This function checks the path against the safe write root and checks if it falls inside the app directory, triggering `prompt_dangerous_approval` where appropriate.
- **Tools Hooking**: `tools/file_tools.py` checks both read and write tools against the containment boundary before performing disk I/O.
