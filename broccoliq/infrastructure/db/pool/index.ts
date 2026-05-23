import { getDb, getRawDb, closeAllShards, getActiveShards } from "../Config.js";
import type { Schema } from "../Config.js";
import { logger } from "../../util/Logger.js";
import { Locker } from "./Locker.js";
import { Mutex } from "./Mutex.js";
import { executeBulkInsert, executeChunkedRawInsert, executeSingleOp, groupOps } from "./Operations.js";
import { applyOpsToResults, postProcessResults } from "./QueryEngine.js";
import { ShardState } from "./ShardState.js";
import { isIncrement, normalizeWhere } from "./types.js";
import type { Increment, WhereCondition, WriteOp } from "./types.js";
import { CompiledQuery } from "kysely";
import type { Kysely } from "kysely";

/**
 * BufferedDbPool provides a high-performance, sharded, asynchronous write-behind layer.
 * It batches operations, manages agent-specific partitions, and ensures data
 * consistency between in-memory Level 7 buffers and on-disk Level 2 storage.
 */
export class BufferedDbPool {
	private shards = new Map<string, ShardState>();
	private agentShadows = new Map<string, { ops: WriteOp[]; affectedFiles: Set<string>; lastUpdated: number }>();
	private stateMutex = new Mutex("DbStateMutex");
	private flushMutex = new Mutex("DbFlushMutex");
	private locker = new Locker(this);
	private flushInterval: NodeJS.Timeout | null = null;
	private cleanupInterval: NodeJS.Timeout | null = null;
	private parameterBuffer = new Array(5000);
	
	public static increment(value: number): Increment {
		return { _type: "increment", value };
	}

	constructor() {
		this.startFlushLoop();
	}

	public getShard(id: string = "main"): ShardState {
		let s = this.shards.get(id);
		if (!s) {
			s = new ShardState(id);
			this.shards.set(id, s);
		}
		return s;
	}

	public async getDb(shardId: string = "main"): Promise<Kysely<Schema>> {
		return getDb(shardId);
	}

	private startFlushLoop() {
		this.flushInterval = setInterval(() => this.flush(), 1000);
		this.cleanupInterval = setInterval(() => this.cleanupShadows(), 30000);
		logger.info("BufferedDbPool initialized with sharded architecture.");
	}

	private async cleanupShadows() {
		const release = await this.stateMutex.acquire();
		try {
			const now = Date.now();
			const SHADOW_EXPIRATION = 5 * 60 * 1000;
			for (const [agentId, shadow] of this.agentShadows.entries()) {
				if (now - shadow.lastUpdated > SHADOW_EXPIRATION) {
					this.agentShadows.delete(agentId);
				}
			}
		} finally {
			release();
		}
	}

	public async beginWork(agentId: string) {
		const release = await this.stateMutex.acquire();
		try {
			if (!this.agentShadows.has(agentId)) {
				this.agentShadows.set(agentId, { ops: [], affectedFiles: new Set(), lastUpdated: Date.now() });
			}
		} finally {
			release();
		}
	}

	public async push(op: WriteOp, agentId?: string, affectedFile?: string) {
		const startTime = performance.now();
		const ops = [op];
		for (const o of ops) {
			if (agentId) o.agentId = agentId;
			o.hasIncrements = o.values ? Object.values(o.values).some(isIncrement) : false;
			if (o.type === "update" && o.where && !Array.isArray(o.where) && o.where.column === "id") {
				o.dedupKey = `${o.table}:${o.where.value}`;
			}

			// Level 7: Index maintenance
			if (o.table === "queue_jobs" && o.values && (o.values as Record<string, unknown>).status && o.where && !Array.isArray(o.where) && o.where.column === "id") {
				const ids = Array.isArray(o.where.value) ? o.where.value.map(String) : [String(o.where.value)];
				const shard = this.getShard(o.shardId);
				
				let tableIndex = shard.activeIndex.get(o.table);
				if (!tableIndex) {
					tableIndex = new Map();
					shard.activeIndex.set(o.table, tableIndex);
				}

				const key = `status:${(o.values as Record<string, unknown>).status}`;
				let idMap = tableIndex.get(key);
				if (!idMap) {
					idMap = new Map();
					tableIndex.set(key, idMap);
				}

				let globalIdMap = shard.activeIndexById.get(o.table);
				if (!globalIdMap) {
					globalIdMap = new Map();
					shard.activeIndexById.set(o.table, globalIdMap);
				}

				for (const jobId of ids) {
					["pending", "processing", "done", "failed"].forEach(s => {
						tableIndex?.get(`status:${s}`)?.delete(jobId);
					});
					idMap.set(jobId, o);
					globalIdMap.set(jobId, o);
				}
			}
		}

		if (agentId) {
			const shadow = this.agentShadows.get(agentId);
			if (shadow) {
				shadow.ops.push(...ops);
				if (affectedFile) shadow.affectedFiles.add(affectedFile);
				shadow.lastUpdated = Date.now();
			}
		} else {
			for (const o of ops) {
				const shard = this.getShard(o.shardId);
				let tableBuffer = shard.activeBuffer.get(o.table);
				if (!tableBuffer) {
					tableBuffer = [];
					shard.activeBuffer.set(o.table, tableBuffer);
				}
				tableBuffer.push(o);
				shard.activeSize++;
			}
		}
		
		const duration = performance.now() - startTime;
		const shard = this.getShard(op.shardId || "main");
		shard.recordLatency("enqueue", duration);

		// Level 11: Memory Backpressure
		// If the shard's active buffer is overloaded, we wait for a moment 
		// to allow the flusher to catch up. This prevents OOM in write-heavy bursts.
		while (shard.isOverloaded()) {
			await new Promise(resolve => setTimeout(resolve, 100));
		}
	}


	public async commitWork(agentId: string) {
		const release = await this.stateMutex.acquire();
		try {
			const shadow = this.agentShadows.get(agentId);
			this.agentShadows.delete(agentId);
			if (shadow) {
				for (const op of shadow.ops) {
					await this.push(op);
				}
			}
		} finally {
			release();
		}
	}

	public async flush() {
		const releaseFlush = await this.flushMutex.acquire();
		const startTime = Date.now();
		try {
			const activeShards: ShardState[] = [];
			const releaseState = await this.stateMutex.acquire();
			try {
				for (const shard of this.shards.values()) {
					if (shard.activeSize > 0) {
						shard.swapToInFlight();
						activeShards.push(shard);
					}
				}
			} finally {
				releaseState();
			}

			if (activeShards.length === 0) return;

			let totalFlushed = 0;
			for (const shard of activeShards) {
				const db = await this.getDb(shard.shardId);
				const rawDb = await getRawDb(shard.shardId);
				const ops = Array.from(shard.inFlightBuffer.values()).flat();

				try {
					await db.transaction().execute(async (trx) => {
						const groups = groupOps(ops);
						for (const group of groups) {
							const first = group[0];
							if (!first) continue;
							if (group.length >= 100 && (first.type === "insert" || first.type === "upsert")) {
								totalFlushed += await executeChunkedRawInsert(first.table, group, rawDb as never, this.parameterBuffer);
							} else if (group.length > 1 && (first.type === "insert" || first.type === "upsert")) {
								totalFlushed += await executeBulkInsert(trx, first.table, group);
							} else {
								for (const op of group) {
									await executeSingleOp(trx, op);
									totalFlushed++;
								}
							}
						}
					});
					shard.clearInFlight();
				} catch (e) {
					logger.error(`[DbPool] Atomic flush failed for shard ${shard.shardId}:`, e);
					// Level 2: Fail-soft - keep in-flight buffer to retry next time
					// shard.clearInFlight(); // DO NOT CLEAR on error, let it retry
					throw e; 
				}
			}

			const duration = Date.now() - startTime;
			const throughput = Math.round(totalFlushed / (duration / 1000 || 0.001));
			if (duration > 50) logger.info(`[DbPool] Synchronized ${totalFlushed} ops in ${duration}ms (${throughput} ops/sec)`);
			
			// Record metrics for the main shard as a proxy for global health
			this.getShard("main").recordLatency("processing", duration);
		} finally {
			releaseFlush();
		}
	}



	public getMetrics() {
		const mainShard = this.getShard("main");
		let activeBufferSize = 0;
		let inFlightOpsSize = 0;
		for (const shard of this.shards.values()) {
			activeBufferSize += shard.activeSize;
			inFlightOpsSize += shard.inFlightBuffer.size;
		}

		return {
			shards: Array.from(this.shards.keys()),
			shadows: this.agentShadows.size,
			activeBufferSize,
			inFlightOpsSize,
			latencies: {
				processing: {
					p95: mainShard.calculatePercentile("processing", 95),
					p99: mainShard.calculatePercentile("processing", 99),
				},
				enqueue: {
					p95: mainShard.calculatePercentile("enqueue", 95),
					p99: mainShard.calculatePercentile("enqueue", 99),
				}
			}
		};
	}

	public async selectWhere<T extends keyof Schema>(
		table: T,
		where: WhereCondition | WhereCondition[],
		agentId?: string,
		options?: { orderBy?: { column: string; direction: "asc" | "desc" }; limit?: number; offset?: number; shardId?: string },
	): Promise<Schema[T][]> {
		const shardId = options?.shardId || "main";
		const shard = this.getShard(shardId);
		const conditions = normalizeWhere(where);
		const db = await this.getDb(shardId);

		const release = await this.stateMutex.acquire();
		try {
			// Level 2: Load from Disk
			let query = db.selectFrom(table as any).selectAll() as any;
			for (const cond of conditions) {
				const op = cond.operator || "=";
				query = query.where(cond.column as any, op as any, cond.value as any);
			}
			if (options?.limit) query = query.limit(options.limit);
			if (options?.offset) query = query.offset(options.offset);

			const diskResults = await query.execute() as Schema[T][];

			// Level 7: Apply Memory Buffers
			const finalResults = [...diskResults];
			applyOpsToResults(table, shard.inFlightBuffer, shard.inFlightIndex, finalResults, conditions, shard.warmedIndices, shardId);
			applyOpsToResults(table, shard.activeBuffer, shard.activeIndex, finalResults, conditions, shard.warmedIndices, shardId);

			if (agentId) {
				const shadow = this.agentShadows.get(agentId);
				if (shadow) {
					const shadowMap = new Map<keyof Schema, WriteOp[]>();
					shadowMap.set(table, shadow.ops.filter(op => op.table === table && (op.shardId || "main") === shardId));
					applyOpsToResults(table, shadowMap, undefined, finalResults, conditions, shard.warmedIndices, shardId);
				}
			}

			return postProcessResults(finalResults, options);
		} finally {
			release();
		}
	}

	public async selectOne<T extends keyof Schema>(table: T, where: WhereCondition | WhereCondition[], agentId?: string, options?: { shardId?: string }): Promise<Schema[T] | null> {
		const results = await this.selectWhere(table, where, agentId, { ...options, limit: 1 });
		return results[0] || null;
	}

	public async acquireLock(resource: string, author: string, shardId: string = "main", ttlMs: number = 30000): Promise<boolean> {
		return this.locker.acquireLock(resource, author, shardId, ttlMs);
	}

	public async releaseLock(resource: string, author: string, shardId: string = "main") {
		return this.locker.releaseLock(resource, author, shardId);
	}

	public async warmupTable<T extends keyof Schema>(table: T, statusCol: string, statusValue: string, shardId: string = "main"): Promise<number> {
		const db = await this.getDb(shardId);
		const rows = await db.selectFrom(table as any).where(statusCol as any, "=", statusValue as any).selectAll().execute() as Schema[T][];
		if (rows.length === 0) return 0;

		const shard = this.getShard(shardId);
		let tableIndex = shard.activeIndex.get(table);
		if (!tableIndex) {
			tableIndex = new Map();
			shard.activeIndex.set(table, tableIndex);
		}

		const key = `status:${statusValue}`;
		let idMap = tableIndex.get(key);
		if (!idMap) {
			idMap = new Map();
			tableIndex.set(key, idMap);
		}

		for (const row of rows) {
			const jobId = String((row as Record<string, unknown>).id);
			idMap.set(jobId, { type: "insert", table, values: row as Record<string, unknown>, shardId });
		}
		shard.warmedIndices.add(`${shardId}:${table as string}:status:${statusValue}`);
		return rows.length;
	}

	public async stop() {
		logger.info("[DbPool] Powering down Sovereign Infrastructure...");
		if (this.flushInterval) clearInterval(this.flushInterval);
		if (this.cleanupInterval) clearInterval(this.cleanupInterval);
		this.flushInterval = null;
		this.cleanupInterval = null;

		// Final authoritative flush and physical connection closure
		try {
			await this.flush();
			
			// Level 11: WAL Checkpointing (Physically merge -wal files)
			const activeShards = getActiveShards();
			for (const shardId of activeShards) {
				try {
					const db = await this.getDb(shardId);
					const result = await db.executeQuery(CompiledQuery.raw("PRAGMA wal_checkpoint(TRUNCATE);"));
					logger.info(`[DbPool] Shard ${shardId} checkpointed: ${JSON.stringify(result.rows[0])}`);
				} catch (e) {
					logger.error(`[DbPool] Failed to checkpoint shard ${shardId}:`, e);
				}
			}
			
			logger.info("[DbPool] Final synchronization and checkpointing successfully committed.");
		} catch (e) {
			logger.error("[DbPool] Critical: Final synchronization failed during shutdown.", e);
		}

		await closeAllShards();
		this.locker.destroy();
		logger.info("[DbPool] Physical resources released (Level 11). System Offline.");
	}
}

export const dbPool = new BufferedDbPool();
