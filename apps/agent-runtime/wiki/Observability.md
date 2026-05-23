# Telemetry, Observability & Tracing 📊

This guide provides operators with everything necessary to inspect, audit, and debug session operations in the production environment.

```text
  [ Ephemeral Logs ] ──> [ Google Cloud Logging ] (Standard Out/Err)
  [ Audit Logs ]     ──> [ Firestore /sessions/{id}/events ] (Chronological JSON)
  [ Artifacts ]      ──> [ Cloud Storage Buckets ] (.diff / snapshots)
```

---

## 1. Real-time Firestore Event Streaming

The Control Plane exposes a live timeline of events. Every operational phase records progress in the `/events` subcollection under `/sessions/{sessionId}/events/`:

* **`info`:** Standard operational transitions (e.g. `Control Plane: Enqueuing task to wake up worker container...`).
* **`success`:** Successful completion of tasks (e.g. `Unit tests completed successfully with 100% coverage`).
* **`warn`:** Warnings that do not abort execution (e.g. `Execution budget usage approaching 80%`).
* **`error`:** Errors that abort and trigger rollbacks (e.g. `Unit tests failed! Rolling back workspace mutations...`).

---

## 2. Cloud Tasks & Cloud Run Container Tracing

If workers do not appear to launch, use these Google Cloud Console paths to diagnose:

### A. Cloud Tasks Queue Status
1. Navigate to **Cloud Tasks** in the GCP Console.
2. Select your regional queue `operator-queue`.
3. Check the **Queue Metrics** graph for:
   * **Active Tasks:** High task count indicates queue congestion.
   * **Error Rate:** Look for non-2xx status codes from the target worker trigger URL.

### B. Cloud Run Job Logging
1. Navigate to **Cloud Run** and choose your service `operator-api` or job `operator-worker`.
2. Open the **Logs** tab.
3. Filter search strings for:
   * `resource.type="cloud_run_revision"`
   * `severity>=WARNING` to find stack traces or execution errors.

---

## 3. GCS Artifact Bucket Layout

Snapshots, patch proposals, and test outputs are structured inside the GCS bucket `gs://operator-artifacts-<project_id>/` as follows:

```text
sessions/
└── {sessionId}/
    ├── preflight.json      # Original workspace manifest & intent details
    ├── proposal.diff       # Generated code patch (SHA-256 bound)
    ├── pre-test.tar.gz     # Zip archive of workspace BEFORE running test suite
    ├── post-test.tar.gz    # Zip archive of workspace AFTER running test suite
    └── test-results.json   # Output and exit code of the npm/pytest command
```
