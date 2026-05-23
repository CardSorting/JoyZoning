# Deployment Guide 🚀

This page outlines the steps required to deploy the DietCode Control Plane to Google Cloud Platform (GCP) and Firebase.

## Prerequisites

Before starting, ensure you have the following installed on your machine:
* [Google Cloud SDK (gcloud CLI)](https://cloud.google.com/sdk/docs/install)
* [Firebase CLI](https://firebase.google.com/docs/cli)
* Node.js & npm (v20+ recommended)

Make sure you are logged in to both CLIs and have selected your target project:
```bash
gcloud auth login
gcloud auth application-default login
firebase login --reauth
```

---

## 1. Automated GCP Infrastructure Deployment

The deployment of GCP services is automated through the `./scripts/deploy-gcp.sh` script.

### What it does:
1. **Enables APIs:** `run.googleapis.com`, `cloudtasks.googleapis.com`, `cloudbuild.googleapis.com`, `firestore.googleapis.com`, and `storage.googleapis.com`.
2. **Creates GCS Bucket:** Provisions `gs://operator-artifacts-<project_id>` in the `us-central1` region to house differential code changes.
3. **Creates Cloud Tasks Queue:** Provisions the queue `operator-queue` to schedule worker containers asynchronously.
4. **Triggers Cloud Build:** Invokes `cloudbuild.yaml` to compile, push, and deploy your FastAPI server image on Cloud Run.

### Run Command:
```bash
./scripts/deploy-gcp.sh
```

---

## 2. Automated Firebase Hosting & Rules Deployment

Deploying firestore security rules, indexes, and compiling the static React assets is automated through the `./scripts/deploy-firebase.sh` script.

### What it does:
1. **Sets project context:** Binds to the active `gcloud` project.
2. **Builds Frontend:** Runs `npm run build` inside the `web` folder, generating index and asset chunks in `hermes_cli/web_dist/`.
3. **Deploys:** Pushes the compiled frontend, rules (`firestore.rules`), and empty indexes (`firestore.indexes.json`) directly to Firebase Hosting.

### Run Command:
```bash
./scripts/deploy-firebase.sh
```

---

## 3. Environment Variables Configuration (`.env`)

Configure your variables by copying `.env.example` to `.env` and adjusting values:

```ini
# GCP Configuration
PROJECT_ID=dreambees-alchemist
GOOGLE_CLOUD_PROJECT=dreambees-alchemist
TASK_REGION=us-central1
QUEUE_NAME=operator-queue

# Cloud Storage Artifact Bucket
ARTIFACT_BUCKET=operator-artifacts-dreambees-alchemist

# Worker execution config (set to true inside Cloud Run)
USE_CLOUD_TASKS=false
WORKER_TRIGGER_URL=https://operator-worker-url-here/trigger
```
