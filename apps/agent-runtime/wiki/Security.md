# Security & Isolation Model 🛡️

DietCode is engineered with a strict **zero-trust, single-tenant, sandboxed execution design** to guarantee that automated code alterations are safely contained, monitored, and bounded.

```text
  [ External Developer API ] ──(Auth Gate)──> [ Control Plane (FastAPI) ]
                                                     │ (Isolated Token)
                                                     ▼
                                            [ Cloud Run Worker Job ]
                                                     │
                                           ┌─────────┴─────────┐
                                           ▼                   ▼
                                    [ Sandbox Jail ]     [ Read-Only IAM ]
                                     (Disposable FS)     (No Admin Roles)
```

---

## 1. Sandbox Isolation & Container Security

To prevent unchecked command execution or network abuse, the **Execution Worker** operates inside a sandboxed Google Cloud Run container using gVisor:

* **Disposable Filesystem:** Every worker phase runs inside a brand-new container instance. Any mutated local files or generated artifacts are uploaded to Google Cloud Storage (GCS) and immediately destroyed upon container exit.
* **No Local State Storage:** No local disk space is persisted between execution steps, preventing any cross-session contamination or persistence-based vulnerabilities.
* **Restricted Network Policies:** The execution worker is permitted external access only to predefined package registries (e.g. `npm`, `pypi`) and the repository's host platform (e.g. `github.com`). Intranet and metadata endpoint lookups are strictly blocked.

---

## 2. IAM Least-Privilege Role Bindings

The GCP service accounts bound to the Control Plane and Execution Worker must strictly follow the principle of least privilege:

### A. Control Plane Service Account (`operator-api`)
* **`roles/firestore.serviceAgent`:** Standard CRUD access to `/sessions` document state and `/events` logs.
* **`roles/cloudtasks.enqueuer`:** Permission to enqueue worker tasks onto the target queue.
* **`roles/storage.objectViewer`:** Read access to pull differential snapshots and preflight templates.

### B. Execution Worker Service Account (`operator-worker`)
* **`roles/firestore.serviceAgent`:** Write-only (append) access to post log events and update session budget usage metrics.
* **`roles/storage.objectAdmin`:** Read/Write access to the specific GCS path `gs://operator-artifacts-<project_id>/sessions/{sessionId}/**` (cannot read or mutate other sessions).
* **`roles/secretmanager.secretAccessor`:** Restricted access only to necessary build API keys (such as `GITHUB_TOKEN`).

---

## 3. Cryptographic Proposal Verification

Before applying any code patch to a production repository workspace, the worker performs a strict safety check:

1. **Hash Verification:** The Control Plane signs the preflight patch proposal by calculating the `SHA-256` hash of the diff content and saving it as `policyHash` inside Firestore.
2. **Signature Comparison:** Upon wakeup, the worker pulls the diff from GCS, recalculates the SHA-256 hash, and compares it to the Firestore `policyHash`.
3. **Execution Gate:** If the recalculated hash does not match, the worker flags an immediate security violation, logs the breach to Firestore `/events`, and terminates without applying changes.
