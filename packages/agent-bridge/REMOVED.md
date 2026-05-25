# `@joyzoning/agent-bridge` — removed

This package previously wrapped the LegacyRuntimeShim HTTP API on port **9090**.

**Do not import `AgentBridgeClient` in new code.** The class constructor throws with a migration message.

Canonical integration:

- JoyZoning control plane: `/api/hermes/health`, managed runs, habitat ingest
- External Hermes: `Hermes:InstallRoot` in control plane config

See `docs/architecture/hermes-runtime-reversal.md`.
