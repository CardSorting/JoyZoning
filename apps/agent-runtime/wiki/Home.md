# DietCode Control Plane Wiki 🪐

Welcome to the **DietCode Control Plane** wiki. This directory contains the reference documentation for the serverless, bounded, single-node operator session model, orchestrating secure developer operations.

## Current Project State: **Alpha Operational** 🚀

We have completed the architectural pivot from distributed-agent frameworks into a highly bounded, predictable, and resilient single-node control-and-execution plane:

```text
               [ Control Plane ]
        React UI ──(Firestore)──> FastAPI API
                                     │ (Cloud Tasks)
                                     ▼
                              [ Execution Plane ]
                        Isolated Worker Subprocess (GCP)
```

### Core Architecture Attributes
* **One Session / One Workspace:** Completely isolated runtime environments. No distributed consensus or swarm coordination overhead.
* **One Mutation Authority:** Cryptographic proposal verification guarantees that code modifications match defined policy bounds before application.
* **Structured Mutations:** Diffs and proposal changes are generated, reviewed, and checkpointed inside sandboxed environments.
* **Bounded Budgets:** Strict controls limit the execution time, patch sizes, file counts, and tool invocations, shutting down processes if limit bounds are crossed.

---

## Wiki Contents

Explore the deep technical documentation to understand, develop, and maintain the operator ecosystem:

| Section | Description |
|---------|-------------|
| 📐 [Architecture](Architecture.md) | Technical topography, data models, and separation of concerns. |
| 🛡️ [Security & Isolation](Security.md) | gVisor containers, restricted network policies, and IAM configurations. |
| 🛡️ [Capability Profiles](Capability_Profiles.md) | Permitted path bounds, package overrides, and YAML profile configurations. |
| 🔄 [Operational Flows](Operational_Flows.md) | 5-phase session transactions, state transitions, and rollbacks. |
| 🔄 [GitOps & CI/CD](GitOps_Integration.md) | Integrating PR pipelines, automatic diff reviews, and GitHub Actions. |
| 📑 [REST API Reference](API_Reference.md) | Endpoint JSON schemas, query/payload limits, and mock responses. |
| 📊 [Observability & Telemetry](Observability.md) | Real-time events, Cloud Tasks status codes, and GCS artifact directory maps. |
| 🚀 [Deployment](Deployment.md) | GCP Cloud Run, Cloud Storage, and Firebase Hosting configuration. |
| 💻 [Development](Development.md) | Local sandbox guides, testing, and contribution protocols. |
| 🩺 [Troubleshooting & Diagnostics](Troubleshooting.md) | Exit code references, stuck queue pipelines, and CORS/403 solutions. |
