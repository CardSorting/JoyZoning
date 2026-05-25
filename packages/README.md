# Legacy packages removed (pass 10)

The following workspace packages were deleted — they only served the retired `:9090` LegacyRuntimeShim:

| Package | Was |
|---------|-----|
| `@joyzoning/agent-bridge` | HTTP client to embedded Hermes |
| `@joyzoning/workspace-core` | Path containment for shim server |
| `@joyzoning/shared-contracts` | TypeScript stubs for shim protocol |

**Use instead:**

- Control plane `/api/hermes/*` and C# domain types
- External Hermes `InstallRoot` for execution and containment

See `docs/architecture/hermes-runtime-reversal.md`.
