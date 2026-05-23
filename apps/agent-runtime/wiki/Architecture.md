# Technical Architecture Guide 📐

DietCode separates the **Control Plane** (human interfaces, policy bindings, authorization check-points) from the **Execution Plane** (sandboxed code generation, testing, mutations).

## Topology Diagram

```text
  [ User Interface / IDE ]
            │ (https)
            ▼
  [ Firebase Hosting (CDN) ] ──(Auth)──> [ Firestore DB ] (Durable State)
            │ (rewrite)                             ▲
            ▼                                       │ (read/write)
  [ Cloud Run: Operator API ] ──────────────────────┘
            │ (enqueue)
            ▼
  [ Cloud Tasks / Queue ]
            │ (dispatch)
            ▼
  [ Cloud Run Job / Worker ] ───────────────────────> [ Cloud Storage ] (Artifacts)
```

---

## 1. The Control Plane
The Control Plane is lightweight, stateless, and optimized for high-speed request/response loops.

* **FastAPI Server (`operator_api.py`):** Acts as the API gateway. Accepts mutations, validates policy hashes, changes document states, and spawns worker tasks.
* **Firestore Database:** Serves as the primary source of truth. All sessions, audit logs, and status records are durably persisted here. No databases are housed on the execution worker itself.
* **Firebase Auth:** Handles user authentication and role-based permissions.

---

## 2. The Execution Plane
The Execution Plane runs asynchronously, executing intensive computational tasks in completely isolated sandboxes.

* **Operator Worker (`operator_worker.py`):** An ephemeral container configured with Node.js and Python. It accepts a `sessionId` and `phase` arguments, pulls the checkout from GCS, generates code changes, runs the project unit test suite, and uploads results.
* **GCS Artifact Bucket:** Durable storage for workspace files, post-test artifacts, and raw proposal diffs (`.diff` files).

---

## 3. Database Schema

### `sessions` (Firestore Document Collection)

```json
{
  "sessionId": "UUIDv4 String",
  "repoName": "String (e.g. NousResearch/hermes-agent)",
  "framework": "String (e.g. React/Vite)",
  "profileName": "String (e.g. nous-native-agent)",
  "instruction": "String (Description of target task)",
  "status": "String (preflight | proposed | applying | success | violation | reverted)",
  "createdAt": "Timestamp",
  "updatedAt": "Timestamp",
  "argsHash": "Cryptographic Sha256 of input parameters",
  "policyHash": "Cryptographic Sha256 of capability permissions",
  "budgetLimit": {
    "maxFiles": 10,
    "maxRuntimeMinutes": 15,
    "maxToolCalls": 25,
    "maxPatchSize": 5000,
    "maxTestRuntimeMinutes": 5
  },
  "budgetUsage": {
    "files": 2,
    "runtime": 3,
    "toolCalls": 7,
    "patchSize": 280,
    "testRuntime": 1
  }
}
```

### `events` (Firestore Subcollection under `/sessions/{sessionId}/events`)

Every status mutation and worker log records a secure audit entry chronologically:

```json
{
  "timestamp": "Timestamp",
  "type": "String (info | success | warn | error)",
  "message": "String (Human-readable description of event)"
}
```
