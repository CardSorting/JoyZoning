# Usage Guide: From First Order to Total Sovereignty 🥦

You've read the manifesto. Now you're ready to build with BroccoliQ. This guide takes you from a basic 7-line setup to a horizontally sharded Level 10 Hive.

---

## 1. Quick Reference: The Escalation Path

| Level | Scale | Implementation |
| :--- | :--- | :--- |
| **Simple Queue** | < 10k ops/sec | Standard `SqliteQueue` (Single Shard) |
| **High Throughput** | 10k - 100k ops/sec | Optimized `concurrency` + `batchSize` |
| **Sovereign Hive** | 1,000,000+ ops/sec | **Level 8 Sharded Partitioning** |

---

## 2. Environment Setup (Bun Native)

BroccoliQ is a **Bun-native** infrastructure layer. While it maintains Node.js compatibility, the system is uniquely optimized for the **Bun engine's zero-latency N-API integration**.

### 🔥 High-Performance Bun Setup (Recommended)
```bash
bun add @noorm/broccoliq
bun run index.ts
```

### Universal Node.js Setup
```bash
npm install @noorm/broccoliq better-sqlite3
node index.js
```

---

## 3. Your First Sovereign Queue (7 Lines)

The complexity of Level 10 is hidden behind a simple, authoritative API.

```typescript
import { SqliteQueue } from '@noorm/broccoliq';

// Initialize with 500 parallel workers
const hive = new SqliteQueue({ concurrency: 500 });

// 0ms Latency: Level 7 Memory-First Enqueue
await hive.enqueue({ task: 'synthesize_knowledge', priority: 10 });

// Start the Hive Processor
hive.process(async (job) => {
  console.log(`[Hive] Processing job ${job.id}`);
}, { concurrency: 500, batchSize: 1000 });
```

---

## 4. API Reference (Axiomatic Cheat Sheet)

### `SqliteQueue<T>` (Level 7 & 8)

| Method | Description | Level |
| :--- | :--- | :--- |
| `enqueue(payload, options?)` | Push job to queue (0ms latency). | L7 |
| `enqueueBatch(items)` | Push multiple jobs in a single transaction. | L6 |
| `process(handler, options?)` | Start high-concurrency individual processing. | L7 |
| `processBatch(handler, options?)` | Start true batch processing (10x faster). | L7 |
| `reclaimStaleJobs()` | Recover jobs stuck in 'processing' (Crash recovery). | L9 |
| `performMaintenance()` | Run physical audits and self-healing. | L9 |

### `BufferedDbPool` (Level 3, 4 & 5)

| Method | Description | Level |
| :--- | :--- | :--- |
| `push(op, agentId?)` | Buffer a write operation (insert/update/delete). | L3 |
| `beginWork(agentId)` | Initialize an **Agent Shadow** (Isolated workspace). | L2 |
| `commitWork(agentId)` | Atomic commit of all shadow operations. | L4 |
| `selectWhere(table, where, agentId?)` | **Reactive Query**: Merges memory + disk results. | L7 |
| `acquireLock(resource, author)` | Global **Sovereign Lock** (Cross-process). | L5 |
| `flush()` | Authoritative synchronization of all buffers to disk. | L4 |

---

## 5. Schema Sovereignty (Level 10)

The v2.1.0 update introduces the **Axiomatic Master Schema**. All core Hive tables are prefixed with `hive_`.

```typescript
import type { Schema } from '@noorm/broccoliq';
import { dbPool } from '@noorm/broccoliq';

// Strongly-typed reactive query
const tasks = await dbPool.selectWhere('hive_tasks', { 
  column: 'status', 
  value: 'pending' 
});

// tasks is now typed as Schema['hive_tasks'][]
console.log(tasks[0]?.id);
```

**Common Hive Tables:**
- `hive_knowledge`: Core context and knowledge nuggets.
- `hive_tasks`: System-level orchestration and status.
- `hive_audit`: Physical security and state-change logs.
- `hive_session`: Agent session metrics and joy caches.

---

## 6. Pro-Grade Configuration Patterns

### The "High-Burst" Sink (1,000,000+ ops/sec)
```typescript
const queue = new SqliteQueue({
  concurrency: 2000,
  batchSize: 10000,           // Dequeue in massive chunks
  shardId: 'ingest-shard-1'   // Level 8 Sharding
});
```

> [!TIP]
> **Memory-First Scaling**: `SqliteQueue` maintains an internal **1,000,000 slot** circular buffer. Jobs are enqueued at memory speeds and flushes are pipelined to the shard's WAL journal.

### The "Warmed" Shard (Extreme Low Latency)
Pre-load status indexes into RAM to avoid the first-query "disk cold start."
```typescript
await dbPool.warmupTable('hive_tasks', 'status:pending');
```

---

## 7. Advanced Monitoring & Error Handling

### Monitoring the Hive
Use `getMetrics()` to observe the physical health and memory pressure of your shards.

```typescript
const metrics = dbPool.getMetrics();

console.log(`Hive Health:
- Active Buffer: ${metrics.activeBufferSize} ops
- Shard Latency: ${metrics.latencies.processing.p99}ms
- Shadows:       ${metrics.shadowCount} active agents
`);
```

### Production-Grade Agent Shadows
Always use a `try...catch...finally` block with **Agent Shadows** to ensure resources are handled correctly.

```typescript
const agentId = `worker-${process.pid}`;

try {
  await dbPool.beginWork(agentId);
  
  // High-speed memory operations
  await dbPool.push({ table: 'hive_tasks', type: 'insert', ... }, agentId);
  
  // Atomic delivery to the shard
  await dbPool.commitWork(agentId);
} catch (err) {
  console.error("Agent failed to commit", err);
}
```

---

## 8. The Production Checklist

- [ ] **Graceful Shutdown**: Always call `await queue.stop()` or `await dbPool.stop()`.
- [ ] **Shard Monitored**: Ensure NVMe throughput isn't hitting physical limits.
- [ ] **Type Checked**: Always use the `Schema` type for total axiomatic safety.
- [ ] **Integrity Enabled**: `IntegrityWorker` is running in the background.

---

**Status**: `Usage Guide Hardened` | **Level**: `10` | **Recipes**: [`COOKBOOK.md`](COOKBOOK.md)