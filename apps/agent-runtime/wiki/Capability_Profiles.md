# Capability Profiles & Policy Verification 🛡️

Capability Profiles define the precise execution boundaries, system access levels, and file mutation policies permitted for a session. Every operator task must bind to a Capability Profile, which is cryptographically verified prior to execution.

```text
  [ User Intent ]
         │
         ▼
  [ Policy Engine ] ──(Checks Capability Profile)──> [ Sandbox Jail ]
                                                         │
                                        ┌────────────────┼────────────────┐
                                        ▼                ▼                ▼
                                 [ File Paths ]   [ Max Patch ]   [ Banned Shell ]
```

---

## 1. Under the Hood: The Policy Engine

Before any workspace file is mutated, the worker checks the code modifications against the session's active **Capability Profile**. The validation check verifies:

1. **Path Restrictions:** Restricts edits only to specific folders (e.g. `web/src/**` or `src/components/**`).
2. **Patch Size Limits:** Enforces maximum size bounds of a single patch to prevent runaway generative code edits.
3. **Banned Shell Commands:** Restricts unit tests or subprocess tasks from calling dangerous shell operations (e.g. `rm -rf`, `curl`, `wget`, `chmod`).

---

## 2. Standard Profile Templates

Capability Profiles are stored inside your configuration files as declarative YAML manifests. Here are the three standard industrial profiles provided with the control plane:

### A. `frontend-polisher` (Standard UI Operations)
Designed for frontend components and style refinements. Very tight boundaries.

```yaml
name: frontend-polisher
description: Restricts mutations strictly to UI components and asset configurations.
permissions:
  allowed_paths:
    - "web/src/components/**"
    - "web/src/pages/**"
    - "web/src/App.tsx"
    - "web/src/index.css"
  blocked_paths:
    - "web/package.json"
    - "web/vite.config.ts"
    - "scripts/**"
  shell:
    banned_commands:
      - "curl"
      - "wget"
      - "sudo"
      - "chmod"
    allowed_commands:
      - "npm run build"
      - "npm test"
  limits:
    max_files: 5
    max_patch_size_bytes: 50000
    timeout_seconds: 300
```

### B. `dependency-manager` (Package & Library Integration)
Permits changes only to dependency manifests to isolate library integrations.

```yaml
name: dependency-manager
description: Allows modifications only to package.json and lock files to run library syncs.
permissions:
  allowed_paths:
    - "package.json"
    - "package-lock.json"
    - "web/package.json"
    - "web/package-lock.json"
  blocked_paths:
    - "src/**"
    - "web/src/**"
  shell:
    banned_commands:
      - "sh"
      - "bash"
    allowed_commands:
      - "npm install"
      - "npm ci"
  limits:
    max_files: 2
    max_patch_size_bytes: 10000
    timeout_seconds: 600
```

### C. `full-operator` (Unrestricted Maintenance)
Permits full code renovations, reserved for audited administrators.

```yaml
name: full-operator
description: Unrestricted maintenance permissions across all files and systems.
permissions:
  allowed_paths:
    - "**"
  blocked_paths: []
  shell:
    banned_commands: []
    allowed_commands:
      - "*"
  limits:
    max_files: 50
    max_patch_size_bytes: 1000000
    timeout_seconds: 1800
```

---

## 3. Policy Violation Lifecycle

If a worker detects that a patch violates the bounds of its active Capability Profile:

1. **Aborts Mutation:** The worker halts before touching any workspace file.
2. **Logs Audit Event:** Appends an `error` log event to `/events` (e.g. `Policy violation: Modification to web/package.json is blocked under 'frontend-polisher' profile`).
3. **Applies Violation State:** Updates Firestore `/sessions/{id}` status to `violation`.
4. **Clean Exit:** Terminates the container process gracefully with exit code `0` to signal to Cloud Tasks that the pipeline executed safely.
