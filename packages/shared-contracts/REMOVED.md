# `@joyzoning/shared-contracts` — legacy TypeScript stubs

These interfaces powered the removed `:9090` AgentBridge protocol. JoyZoning now uses:

- **C# domain types** (`JoyZoning.Domain`, `NormalizedAgentEvent`, control plane DTOs)
- **Control plane HTTP** (`/api/hermes/*`, habitat ingest)

Do not add new TypeScript consumers. The package may be deleted in a future cleanup once lockfile references are gone.
