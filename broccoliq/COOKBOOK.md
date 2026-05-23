# The Sovereign Cookbook: Practical Hive Recipes (v2.1.0) 🥦

This cookbook provides **practical, copy-pasteable code recipes** for solving common problems with the BroccoliQ Hive. Each recipe is modernized for the **Standardized Levels of Sovereignty**.

---

## Recipe 1: Basic Sharded Job Queue (Level 8)

### Use Case
Enqueue items into partitioned shards and process them at scale with dedicated IO bandwidth.

```typescript
import { SqliteQueue } from '@noorm/broccoliq';

interface EmailJob {
  to: string;
  subject: string;
}

class EmailService {
  // Sharding by category for horizontal IO bandwidth (Level 8)
  private queue = new SqliteQueue<EmailJob>({ 
    shardId: 'transactional-emails', 
    concurrency: 500 
  });

  async sendEmail(to: string, subject: string) {
    // Level 7 Memory-First Enqueue (0ms latency)
    const jobId = await this.queue.enqueue({ to, subject }, {
      priority: 10,
    });

    console.log(`[Hive] Job ${jobId} injected into 'transactional-emails' shard`);
    return jobId;
  }

  async startWorker() {
    this.queue.process(async (job) => {
      // Level 7 Reactive Indexing: Hits RAM first
      console.log(`[Worker] Sending email to ${job.to}: ${job.subject}`);
      await actualSmtpSend(job);
    }, { 
      concurrency: 100,
      pollIntervalMs: 1 
    });
  }
}
```

---

## Recipe 2: Delayed Hive Operations (Level 7)

### Use Case
Schedule tasks to run at a specific future time. The Hive won't poll the disk for these until it's time.

```typescript
import { SqliteQueue } from '@noorm/broccoliq';

class MaintenanceHive {
  private queue = new SqliteQueue();

  async scheduleCleanup(resourceId: string, runAt: Date) {
    const delayMs = runAt.getTime() - Date.now();

    await this.queue.enqueue({ action: 'delete', resourceId }, {
      id: `cleanup-${resourceId}`,
      delayMs, // Level 7 Scheduling
    });
  }

  async startMaintenance() {
    this.queue.process(async (job) => {
      console.log(`[Hive] Executing delayed cleanup for ${job.resourceId}`);
      await deleteResource(job.resourceId);
    });
  }
}
```

---

## Recipe 3: Agent Shadow Atomic Operations (Level 4)

### Use Case
Perform multiple database operations as a single atomic unit without holding long-running database locks.

```typescript
import { dbPool } from '@noorm/broccoliq';

class KnowledgeAgent {
  async processThought(agentId: string, thought: string, contextId: string) {
    // 1. Begin Sovereign Autonomy (Level 2)
    await dbPool.beginWork(agentId);

    try {
      // 2. Push operations into the Agent's Shadow Buffer (Memory-only)
      await dbPool.push({
        table: 'hive_knowledge',
        type: 'insert',
        values: { content: thought, context_id: contextId }
      }, agentId);

      await dbPool.push({
        table: 'hive_tasks',
        type: 'update',
        values: { last_active: Date.now() },
        where: { column: 'id', value: agentId }
      }, agentId);

      // 3. Atomic Commit (Level 4): Moves shadow to Shard Buffers
      await dbPool.commitWork(agentId);
      console.log(`[Sovereign] Agent ${agentId} synthesized knowledge atomically.`);
    } catch (err) {
      // No explicit rollback needed; uncommitted shadows expire automatically.
      console.error(`[Failure] Agent ${agentId} work aborted.`);
    }
  }
}
```

---

## Recipe 4: Builder's Punch (Level 6)

### Use Case
Ingest over 1,000,000 operations per second using batching and operation merging.

```typescript
import { SqliteQueue } from '@noorm/broccoliq';

class IngestHive {
  private queue = new SqliteQueue({ shardId: 'ingest' });

  async handleBurst(events: any[]) {
    // Level 6: enqueueBatch allows the Hive to utilize the "Builder's Punch"
    // merging multiple writes into single transactions.
    const ids = await this.queue.enqueueBatch(events.map(e => ({
      payload: e,
      priority: 5
    })));
    
    console.log(`[Burst] Injected ${ids.length} events at CPU velocity.`);
  }
}
```

---

## Recipe 5: Hive-Wide Physical Audit (Level 9)

### Use Case
Manually trigger the self-healing and integrity checks across all shards.

```typescript
import { IntegrityWorker } from '@noorm/broccoliq';

async function performSecurityAudit() {
  const worker = new IntegrityWorker();
  
  // Scans all registered physical WAL journals for corruption (Level 9)
  await worker.performPhysicalAudit();
  
  // Reclaims jobs stuck in 'processing' state (Crash recovery)
  await worker.reclaimStaleJobs();
}
```

---

## 👨‍🍳 Advanced Ingredients
Refer to [ARCHITECTURE_EXPLAINED.md](ARCHITECTURE_EXPLAINED.md) for internal details on **Level 7 Reactive Indexing** and **Level 8 Shard Lifecycle**.
