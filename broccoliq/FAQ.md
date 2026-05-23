# Deep Dive: The Sovereign FAQ 🥦

This document addresses nuances regarding the **BroccoliQ Level 10 Hive**. It covers sharding, agent autonomy, and professional-grade persistence patterns.

---

## 🏛️ General Hive Questions

### Q: Why move from a monolithic pool to a modular sharded architecture?
**A:** Scalability and maintainability. By modularizing into `ShardState`, `QueryEngine`, and `Operations`, we achieved:
1. **Level 8 Sharding**: Horizontal partition across multiple WAL journals for linear IO growth.
2. **Level 10 Hardening**: Strict, zero-`any` type safety via the v2.1.0 unified schema.
3. **Operational Clarity**: Separation of concerns between buffer-management, reactive-indexing, and SQL-generation.

### Q: Does BroccoliQ replace my primary database?
**A:** No. BroccoliQ is a **High-Performance Ingestion and Coordination Hive**. It absorbs 1,000,000+ ops/sec and serializes them into durable physical shards. Consider it the "Adrenal Gland" of your infrastructure.

---

## 🚀 Performance & Sharding (Level 8)

### Q: When should I start sharding? (Level 8)
**A:** **Shard early, shard often.** While a single shard can handle ~50,000 ops/sec, sharding allows you to bypass the physical IO wall of the filesystem. If you have naturally isolated domains (e.g., separate projects), give them each a `shardId`.

### Q: What is the "Builder's Punch" (Level 6)?
**A:** It is our internal high-scale increment engine. When multiple `increment()` operations hit the same counter in-memory, the `Operations.ts` component merges them into a single update. 1,000 concurrent +1 increments become **one single +1000 database operation**.

---

## 🛡️ Agent Autonomy & Consistency

### Q: Why does locking feel slower than enqueuing?
**A:** Because **Sovereign Locking (Level 5)** uses **Direct Consistency (Level 2)**. Enqueuing is buffered at Level 7 for asynchronous delivery, allowing 0ms latency. Locking requires immediate database acknowledgement to ensure multiple agents don't claim the same resource.

### Q: How do I resolve first-query latency?
**A:** Use **Table Warming**. Call `dbPool.warmupTable()` to pre-load critical status indexes into the Level 7 Reactive Index. This ensures even the very first worker query hits RAM at O(1) speed.

### Q: Why use Agent Shadows instead of transactions? (Level 4)
**A:** Standard database transactions often lead to long-held locks. **Agent Shadows** (`beginWork`/`commitWork`) provide explicit autonomy:
- Agents compute in their own private memory space.
- They only interact with the Shard Buffers during the `commitWork` delivery phase.
- This results in **Zero-Contention** for 99% of the agent's execution lifecycle.

---

## 💎 Integrity & Sustainability

### Q: How does the "Self-Healing" mechanism work? (Level 9)
**A:** The **Integrity Worker** performs periodic **Physical Audits**. It scans shards for corruption, reclaims orphans from crashed agents (via `visibilityTimeoutMs`), and prunes stale telemetry based on your `pruneDoneAgeMs` policy.

---

**Status**: `Sovereign FAQ Hardened` | **Level**: `10` | **Version**: `2.1.0`