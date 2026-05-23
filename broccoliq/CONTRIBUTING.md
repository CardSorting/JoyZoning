# Contributing to the Sovereign Hive 🥦

Welcome, Agent. By contributing to BroccoliQ, you are helping build the most sharded, modular, and Level 10 hardened persistence infrastructure in the ecosystem.

---

## 💎 The Level 10 Standard

All contributions must adhere to the **Architecture Principles of Sovereignty**:

1.  **Zero `any` Usage**: Type safety is non-negotiable. Every data path must be strictly typed or use `unknown` with explicit validation.
2.  **Modular Isolation**: Modifications to the `BufferedDbPool` must respect the 4-component boundaries (**ShardState**, **Operations**, **QueryEngine**, **Locker**).
3.  **Agent Shadow First**: All multi-step transactional logic must be implemented via **Agent Shadows** to prevent monolithic Hive locking.
4.  **Sharded Validation**: All new features must be verified with sharded test cases. Single-shard results are insufficient for Level 10 approval.

---

## 🛠️ Performance & Hardening

- **CPU Velocity First**: Avoid large object allocations in the hot path. Use the pre-allocated parameter buffers in `Operations.ts` where possible.
- **Async Sovereignty**: Never block the event loop. Hive flushes must remain background-priority heartbeats.
- **Zero-Shim Principle**: No high-level convenience shims are allowed if they obscure the granular power of the Hive.

---

## 🧪 Testing Protocol

Run all tests before submitting a Pull Request:

```bash
# Standard tests
npm test

# Benchmarking for regression (Latency must not increase)
npm run benchmark
```

---

## 🤝 Code of Conduct

We follow a high-standard, professional conduct protocol. See [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) for details.

**Your contribution is the pulse of the Hive. Build with Sovereignty.**
