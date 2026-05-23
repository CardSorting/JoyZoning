/**
 * BroccoliQ - Queue and Database Processing Infrastructure
 *
 * Re-exports the main public API for easy import from '@broccoliq/queue'.
 * Use direct imports from 'broccoliq' for the primary entry points.
 */

// Core database and pool functionality
export { setDbPath, getDb, getRawDb } from "./db/Config.js";
export { dbPool, BufferedDbPool } from "./db/pool/index.js";
export { IntegrityWorker } from "./db/IntegrityWorker.js";

// Signaling and Queue functionality
export { Signaling } from "./queue/Signaling.js";
export { SqliteQueue } from "./queue/SqliteQueue.js";
export type { Schema } from "./db/DatabaseSchema.js";
