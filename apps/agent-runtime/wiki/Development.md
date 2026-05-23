# Development Guide 💻

This guide describes how to run and test the DietCode Control Plane locally in your development sandbox.

## 1. Local FastAPI Development Server

To run the `operator_api` server locally:

1. **Activate Virtual Environment:**
   ```bash
   source .venv/bin/activate
   ```

2. **Launch Uvicorn Server:**
   ```bash
   uvicorn operator_api:app --host 127.0.0.1 --port 8080 --reload
   ```

The Swagger docs are available locally at [http://127.0.0.1:8080/docs](http://127.0.0.1:8080/docs).

---

## 2. Simulating Worker Phases Locally

In local development mode (`USE_CLOUD_TASKS=false`), the FastAPI server automatically spawns `operator_worker.py` as a background subprocess.

You can also trigger specific worker phases manually from your terminal for debugging:

```bash
# Trigger the Preflight validation and Diff Proposal phase
python operator_worker.py <sessionId> preflight

# Trigger the Mutation and unit Test execution phase
python operator_worker.py <sessionId> apply
```

### What happens in mock phases:
* **`preflight`:** Verifies the safety hashes, generates a mockup `proposal.diff` patch, uploads it to GCS, and marks the session status as `proposed`.
* **`apply`:** Reconstitutes the patch, runs unit tests, verifies budget constraints, and transitions the session status to `success` or `violation`.

---

## 3. Active Budget Boundary Checking

The worker validates resource parameters before executing tests:
1. **File Count Boundary:** If mutated files exceed `maxFiles`, the worker cancels operation, updates Firestore status to `violation`, and rolls back mutations to restore the original sandbox.
2. **Runtime Verification:** Checks execution times, failing gracefully if processes run too long.

---

## 4. Real-time Observability & Debugging

Open the [Firebase Firestore Console](https://console.firebase.google.com/) under the `dreambees-alchemist` project to monitor runs:
* **`/sessions/{sessionId}`:** Watch properties like `status`, `budgetUsage.files`, and `budgetUsage.runtime` update dynamically in real time.
* **`/sessions/{sessionId}/events`:** View chronological, structured audit events logged by the API and worker containers.
