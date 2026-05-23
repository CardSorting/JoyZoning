# Troubleshooting & Run Diagnostics 🩺

Use this guide to diagnose and resolve errors, failed runs, or connectivity problems in the DietCode Control Plane.

---

## 1. Firebase Hosting & API Gateway Issues

### ❌ Error: "HTTP 403: Cloud Run Admin API has not been used... or it is disabled"
* **Symptom:** Deploying to Firebase Hosting fails on version finalization.
* **Cause:** The Firebase rewrite directs traffic to Cloud Run, but the Cloud Run API is not enabled in your GCP project.
* **Resolution:**
  1. Open the [GCP console URL from the error log](https://console.developers.google.com/apis/api/run.googleapis.com/overview) and click **Enable**.
  2. Alternatively, run:
     ```bash
     gcloud services enable run.googleapis.com
     ```
  3. Re-run `firebase deploy --only hosting`.

### ❌ Error: "HTTP 404: Not Found" on `/api/sessions`
* **Symptom:** Calling API endpoints returns a blank 404 page.
* **Cause:** The Cloud Run `operator-api` service has not finished deploying, or the Firebase Hosting URL was not updated with rewrites.
* **Resolution:**
  1. Verify the service exists:
     ```bash
     gcloud run services list
     ```
  2. Check `firebase.json` for the `/api/**` rewrite pointing to `operator-api`.
  3. Re-run `firebase deploy --only hosting`.

---

## 2. Worker Execution & Pipeline Errors

### ❌ Session Status Stuck in `applying`
* **Symptom:** The console status spinner continues indefinitely.
* **Cause:** Cloud Tasks is unable to wake up the worker container (invalid trigger URL), or the worker is crashing on container startup before updating Firestore.
* **Resolution:**
  1. Inspect Cloud Tasks:
     * Open **Cloud Tasks** in GCP Console.
     * Select `operator-queue`.
     * Click **Tasks** to see if retries are failing and inspect the HTTP status code (e.g. `403 Forbidden`, `503 Service Unavailable`).
  2. Inspect Worker Logs:
     * Navigate to **Cloud Run** in GCP Console.
     * Select the `operator-worker` job.
     * Open the **Logs** tab and search for python import or syntax errors.

### ❌ Error: "Cryptographic verification failed" in Event Timeline
* **Symptom:** Session transitions immediately from `applying` to `violation` with a warning message.
* **Cause:** The file content of `proposal.diff` downloaded from GCS has been mutated, or the calculated SHA-256 does not match the `policyHash` saved during preflight.
* **Resolution:**
  1. Ensure GCS artifacts are not modified after preflight generation.
  2. Check for race conditions where multiple requests attempt to apply changes to the same session simultaneously.

---

## 3. Worker Container Exit Codes Reference

| Exit Code | Cause | Meaning | Resolution |
|-----------|-------|---------|------------|
| `0` | Clean Exit | Phase completed successfully or aborted due to a caught validation limit. | Inspect `/events` to verify if status is `success` or `violation`. |
| `1` | Runtime Crash | Python exception, missing package dependencies, or incorrect firestore permissions. | Check Cloud Run revision logs for traceback details. |
| `137` | Out of Memory | The container exceeded its configured RAM limits (e.g. while building packages). | Increase Cloud Run Job memory limit in `cloudbuild.yaml` (e.g. from `512Mi` to `2Gi`). |
| `124` | Timeout Exceeded | The test suite run exceeded the `maxTestRuntimeMinutes` boundary. | Optimize test suite execution times or increase the limit inside the active Capability Profile. |
