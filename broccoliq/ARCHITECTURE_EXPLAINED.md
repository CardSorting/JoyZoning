# Architecture Explained: How BroccoliQ Actually Works

This chapter peels back the curtain. No more "does it work" questions—this is how the Level 10 Sovereign Hive operates at scale.

---

## Chapter 1: Level 3 & 4 - The Dual Buffer Persistence Logic

### The Myth: "Does the queue use memory or disk?"

**Truth:** It uses **sharded dual-buffering** to orchestrate both.

The BroccoliQ Sovereign Hive is architected specifically for high-throughput sharded WAL journals. While it maintains Node.js compatibility, the system's modular `BufferedDbPool` is designed to leverage multiple independent SQLite files for 1,000,000+ operations per second.

### The "Dual Buffer" Pipeline (Level 3 & 4)
```mermaid
graph LR
  subgraph "Shard Memory State (Level 3)"
    AB[Active Buffer] -- "Swap" --> IB[In-Flight Buffer]
    IB -- "Flush (Level 4)" --> PS[Physical Shard]
  end
  
  W1[Write Operation] -- "push()" --> AB
  RE[Read Engine] -- "query()" --> AB
  RE -- "query()" --> IB
  RE -- "query()" --> PS
  
  style AB fill:#4caf50,color:#fff
  style IB fill:#ff9800,color:#fff
  style PS fill:#2196f3,color:#fff
```

### The Flush Synchronization Lifecycle
To ensure zero-downtime, the `BufferedDbPool` uses two independent Mutexes to orchestrate the swap-and-flush cycle.

```mermaid
sequenceDiagram
    participant F as Flush Loop (setInterval)
    participant FM as Flush Mutex
    participant SM as State Mutex
    participant S as Shard State
    participant Ops as Operations Engine
    
    F->>FM: acquire()
    F->>SM: acquire()
    Note over SM: Protected Swap
    F->>S: swapToInFlight()
    Note right of S: Active -> In-Flight, Active = [ ]
    F->>SM: release()
    Note over FM: Async I/O Ongoing
    F->>Ops: executeChunkedRawInsert()
    Note right of Ops: Disk WAL Journal
    F->>S: clearInFlight()
    F->>FM: release()
```

---

## Chapter 2: Level 2 & 5 - The Locking Bypass (Direct I/O)

### The Myth: "Is locking as fast as enqueuing?"

**Truth:** **No.** Locking requires **Direct Persistence (Level 2)** for absolute coordination.

Unlike enqueuing, which is buffered at Level 7 for eventual delivery, **Sovereign Locking** bypasses the buffers entirely. It uses direct database execution to ensure that every agent in the swarm has an immediate, authoritative view of resource ownership.

### Sequence: The Locking Bypass
```mermaid
sequenceDiagram
    participant A as Agent
    participant P as BufferedDbPool
    participant L as Locker (Direct I/O)
    participant S as Shard (Disk)
    
    Note over A: Needs "README.md" lock
    A->>P: acquireLock('README.md')
    P->>L: Delegate to Locker
    Note right of L: Bypass L7 Buffers
    L->>S: DELETE FROM claims WHERE expires < now
    L->>S: INSERT INTO claims (resource, author)
    S-->>L: Success (L2 Persistent)
    L-->>P: Locked
    P-->>A: Success
    Note over A: Safe to modify README
```

---

## Chapter 3: Level 5 & 8 - The Locking Heartbeat Lifecycle

BroccoliDB uses **heartbeats** to maintain possession of a lock. This ensures that if an agent crashes, the lock is automatically released after the TTL expires.

### Sequence: Heartbeat & Expiration
```mermaid
sequenceDiagram
    participant A as Agent
    participant L as Locker
    participant D as Shard Disk
    participant T as Timer (TTL/2)
    
    A->>L: acquireLock(resource, author, ttl)
    L->>D: DELETE FROM claims WHERE expiresAt < now
    L->>D: INSERT INTO claims (values)
    D-->>L: OK
    Note over L: Start Heartbeat Loop
    T-->>L: Heartbeat Trigger
    L->>D: UPDATE claims SET expiresAt = now + ttl
    Note over A,D: Lock possessed autonomously
    A->>L: releaseLock()
    L->>T: clearInterval()
    L->>D: DELETE FROM claims WHERE path = resource
    D-->>L: OK
    L-->>A: Success
```

---

## Chapter 4: Level 7 - The Triple-Buffer Query Merge

### The Myth: "How do we ensure Read-Your-Writes consistency?"

**Truth:** **The 4-Layer Recursive Merge.**

The `QueryEngine` provides absolute consistency by merging results from progressively more "recent" memory layers before returning them to the agent.

### Sequence: The Merge Priority
```mermaid
sequenceDiagram
    participant A as Agent
    participant QE as QueryEngine (L7)
    participant S as Agent Shadow (L4)
    participant AB as Active Buffer (L3)
    participant IB as In-Flight Buffer (L4)
    participant D as Physical Disk (L2)
    
    A->>QE: selectWhere(table, conditions, agentId)
    QE->>D: Initial query from Disk
    D-->>QE: Raw results
    QE->>IB: applyOpsToResults(In-Flight)
    Note over QE: Merge pending flushes
    QE->>AB: applyOpsToResults(Active)
    Note over QE: Merge current memory
    QE->>S: applyOpsToResults(Shadow)
    Note over QE: Merge private unit of work
    QE-->>A: Authoritative Final Result
```

---

## Chapter 5: Level 7 - Auth-Index Decision Logic

### The Myth: "Does it always scan the buffer?"

**Truth:** **No.** If a table was "warmed," the engine skips the scan entirely for status-filtered queries.

The `QueryEngine` uses **authoritative indices** to provide O(1) status filtering. This logic branch determines whether we can bypass the high-cost buffer scan.

### Flow: Auth-Index Logic Branch
```mermaid
graph TD
    Start[selectWhere Query] --> Filter{Filter by Status?}
    Filter -- No --> Scan[Scan Full Op Buffer]
    Filter -- Yes --> CheckWarm{Index Warmed?}
    CheckWarm -- No --> Scan
    CheckWarm -- Yes --> MapLookup[Authoritative Map Lookup]
    MapLookup --> Return[Return Merged Results]
    Scan --> Return
```

---

## Chapter 6: Level 9 - Autonomous Integrity Lifecycle 🛡️

### The Myth: "What happens if a shard corrupts?"

**Truth:** **The Hive heals itself.**

The `IntegrityWorker` is a background assistant that continuously audits every shard for physical and logical consistency.

### Logic: The Self-Healing Audit Loop
```mermaid
graph TD
    Audit[runAudit Loop] --> Shard[Select Active Shard]
    Shard --> Physical{PRAGMA Check}
    Physical -- Fail --> Alert[Log Critical Corruption]
    Physical -- Pass --> Logical[Dangling Nodes Check]
    Logical -- Orphans Found --> Repair[Auto-Repair Parent Links]
    Logical -- Pass --> Prune[Prune Telemetry > 7 Days]
    Prune --> Maint{Frag > 30%?}
    Maint -- Yes --> Polish[REINDEX & VACUUM]
    Maint -- No --> Done[Finish Shard Audit]
    Repair --> Prune
```

---

## Chapter 7: Level 7 - Pipelined Circular Buffers

### The Myth: "How does the memory buffer stay full?"

**Truth:** **Pipelined Dequeuing.**

`SqliteQueue` doesn't just fetch what you ask for. It proactively fetches **up to 2x the requested batch size** to pre-fill the local circular buffer.

### Visual: Circular Buffer Pointer Mechanics
```mermaid
graph LR
    subgraph "Pending Job Array (1M Slots)"
        H((head)) --- J1[Job A]
        J1 --- J2[Job B]
        J2 --- J3[Job C]
        J3 --- T((tail))
        T --- E[Empty]
        E --- H
    end
    
    H --- Deq["<b>Dequeue (Pop)</b>"]
    T --- Enq["<b>Enqueue (Push)</b>"]
```

---

## Chapter 8: Level 3 & 7 - The Wake-Up Pipeline

To avoid database polling (which kills performance), BroccoliQ uses an `EventEmitter` to wake up worker threads only when there is actually work to do.

### Flow: Wake-Up Signal Pipeline
```mermaid
graph LR
    P[push Job] --> E[wakeUpEmitter.emit]
    E --> RW[runWorker Wait Loop]
    RW -- resolve --> JB[dequeueBatch]
    JB -- Process --> PD[process Job]
    
    subgraph "The Sleep State"
        RW --- T[setTimeout: poll fallback]
    end
```

---

## Chapter 9: Modular Persistence Architecture

To achieve **Level 10 Hardening**, the `BufferedDbPool` is divided into specialized domains:

| Component | Sovereignty Level | Responsibility |
|-----------|-------------------|----------------|
| **Locker.ts** | Level 5 (Global) | Cross-process mutual exclusion via Direct I/O. |
| **ShardState.ts** | Level 8 (Shards) | Life-cycle management of a single partition. |
| **Operations.ts** | Level 3 & 6 | "Builder's Punch" coalescing and RAW SQL execution. |
| **QueryEngine.ts** | Level 7 (Memory) | "Auth-Index" reactive querying and result merging. |

---

## Chapter 10: Level 2 & 4 - Agent Shadow Isolation

Modern BroccoliQ uses **Agent Shadows** for explicit autonomy. These shadows allow agents to perform multiple operations that are only visible to the rest of the Hive once they are **Committed**.

### Sequence: Atomic Shadow Commit
```mermaid
sequenceDiagram
    participant A as Agent
    participant S as Agent Shadows (Map)
    participant B as Shard Buffer (Active)
    participant M as State Mutex
    
    A->>S: push(op, agentId)
    Note over S: Op stored in private memory
    A->>M: acquire() (commitWork)
    M->>S: getOps(agentId)
    S-->>S: delete(agentId)
    S->>B: spliceOpsInPlace(ops)
    M-->>A: release()
    Note over B: Atomically delivered to Hive
```

**Why Shadows Matter:**
- **Zero-Contention**: Agents work in private memory space. They only interact with the Hive during the `commitWork` phase.
- **Shadow Clean-up**: Any uncommitted shadow is automatically expired after 5 minutes by the `cleanupShadows` loop.

---

## Chapter 11: Level 10 - Axiomatic Type Sovereignty

The v2.1.0 update introduced **Unified Schema Sovereignty**. 

- **DatabaseSchema.ts**: The single source of truth for the entire Hive.
- **hive_** prefixing: All core system tables (knowledge, tasks, audit) are now standardized.
- **Hardened Type Safety**: Every query is type-checked at compile-time against the authoritative schema.

---

**Welcome to the Hive. Welcome to Level 10.**