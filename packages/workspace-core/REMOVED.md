# `@joyzoning/workspace-core` — legacy (no active consumers)

`WorkspaceModel` was used by the removed LegacyRuntimeShim (`apps/agent-runtime/server.ts`).

**Canonical containment** for agent file I/O is enforced in **external Hermes** (`tools/file_tools.py`, path security) against `JOY_WORKSPACE_ROOT` / `HERMES_WRITE_SAFE_ROOT`.

This package remains in the workspace for reference only. Do not add new imports.
