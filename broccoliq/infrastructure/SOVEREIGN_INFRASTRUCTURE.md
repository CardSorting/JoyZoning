# 🏛️ Sovereign Hive: BroccoliDB Infrastructure Manifesto

This document outlines the production-hardened infrastructure implemented for BroccoliDB, designed to achieve **Level 11** axiomatic reliability in high-concurrency swarm environments.

## 1. Philosophical Baseline: The Level 11 Standard
BroccoliDB infrastructure operates on the principle of **Axiomatic Reliability**. This means the system is designed to be self-healing, backpressure-aware, and physically safe at the storage layer, assuming that hardware, runtime, or network failures are inevitable.

---

## 2. Core Architectural Pillars

### 📦 Unified Composite Schema
We have eliminated the fragility of standalone `id` columns for relational entities.
- **Implementation**: `branches` and `tags` tables now use `PRIMARY KEY(repoPath, name)`.
- **Benefit**: Ensures bit-for-bit uniqueness at the database level without requiring external coordination.
- **Hardening**: Auto-migrating indices and self-healing schema injections are performed on every startup via `Config.ts`.

### ⚡ BufferedDbPool (Memory-First Write-Behind)
The heart of BroccoliDB's performance and consistency.
- **Sharded Isolation**: Each database shard is its own isolated state container (`ShardState`).
- **Read-Your-Writes**: The `QueryEngine` merges in-memory "active" and "in-flight" buffers with on-disk Level 2 results.
- **Backpressure (Level 11)**: A **10,000-item throttle** prevents OOM crashes. If a buffer exceeds this limit, incoming writes are blocked until the flusher (1s interval) clears the stack.

### 🛡️ Sovereign Integrity Engine
A background "Reaper" that ensures the physical and logical health of the Hive.
- **Chunked Audits**: Audits nodes in 100,000-row batches to prevent I/O saturation.
- **Autonomous Repair**: Automatically detects and restores "orphaned" nodes (missing lineage) using deep relational indices on `parentId`.
- **Relational Integrity**: Cleans up abandoned `knowledge_edges` referring to non-existent nodes.

### 🚦 SqliteQueue (Hardened Job Processing)
- **Safe Serialization**: Implements `try-catch` protected JSON deserialization to prevent "Payload Corruption" crashes.
- **Concurrency Fairness**: Uses a custom `Mutex` with strict **FIFO hand-off** to prevent task starvation in high-concurrency worker pools.

---

## 3. Production Hardening Features

### 🔌 Atomic Shutdown & Physical Closure
The power-down sequence is now an authoritative synchronization event.
1. **Final Flush**: All remaining memory buffers are committed.
2. **WAL Checkpointing**: Executes `PRAGMA wal_checkpoint(TRUNCATE)` to physically merge the Write-Ahead Log into the `.db` file, reducing it to 0 bytes.
3. **Physical Destruction**: Closes all raw SQLite handles and file descriptors, ensuring zero-byte orphans and preventing file locks.

### 🗝️ Axiomatic Locking
The `Locker` was refactored for absolute mutual exclusion.
- **Constraint-Driven**: Uses the `claims` table with `path` as the Primary Key.
- **Atomic Acquisition**: Lock acquisition is a single, atomic `INSERT` operation, eliminating race conditions between release and re-acquisition.

---

## 4. Operational Best Practices

### Verification Suite
The infrastructure includes built-in verification tools used during development:
- `verify_hardening.ts`: Checks composite PKs, queue resiliency, and integrity repairing.
- `verify_backpressure.ts`: Confirms system throttling under extreme load.
- `verify_shutdown.ts`: Confirms WAL merging and connection closure.

### Physical Resource Management
- **Journal Mode**: Fixed to `WAL` for high concurrency.
- **Synchronous**: Set to `NORMAL` for optimal performance-safety balance.
- **Cache Size**: Optimized for sharded performance.

---

> [!IMPORTANT]
> BroccoliDB is now in a **Zero-Error State**. All TypeScript and linting debt has been fully resolved, and the infrastructure is verified for production deployment.
