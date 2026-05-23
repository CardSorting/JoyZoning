import { sql } from "kysely";
import { logger } from "../../util/Logger.js";
import type { IBufferedDbPool } from "./types.js";

/**
 * Level 8: Distributed Lock Manager.
 * Uses the 'claims' table for cross-process mutual exclusion.
 */
export class Locker {
	private activeLocks = new Map<string, { expiresAt: number; interval: NodeJS.Timeout }>();

	constructor(private pool: IBufferedDbPool) {}

	public async acquireLock(
		resource: string,
		author: string,
		shardId: string = "main",
		ttlMs: number = 30000,
	): Promise<boolean> {
		const db = await this.pool.getDb(shardId);
		const now = Date.now();
		const expiresAt = now + ttlMs;

		try {
			// 1. Level 11: Axiomatic Atomic Claim
			// Since 'path' is now the PRIMARY KEY, a single INSERT is truly atomic.
			// We check for expired locks first, then attempt to claim.
			await db.deleteFrom("claims" as any).where("expiresAt", "<", now as any).execute();

			await db.insertInto("claims" as any).values({
				path: resource,
				repoPath: "global",
				branch: "main",
				author,
				timestamp: now,
				expiresAt,
			} as any).execute();

			// 3. Start Heartbeat
			const interval = setInterval(() => this.heartbeatLock(resource, author, shardId, ttlMs), ttlMs / 2);
			this.activeLocks.set(`${shardId}:${resource}`, { interval, expiresAt });
			return true;
		} catch (e: any) {
			// If insert fails due to UNIQUE constraint, someone else has the lock
			return false;
		}
	}

	private async heartbeatLock(resource: string, author: string, shardId: string, ttlMs: number) {
		const lock = this.activeLocks.get(`${shardId}:${resource}`);
		if (!lock) return;

		const now = Date.now();
		const nextExpires = now + ttlMs;

		try {
			const db = await this.pool.getDb(shardId);
			const result = await db.updateTable("claims" as any)
				.set({ expiresAt: nextExpires as any, timestamp: now as any })
				.where("path", "=", resource)
				.where("author", "=", author)
				.executeTakeFirst();
			
			if (BigInt(result.numUpdatedRows) === 0n) {
				// We lost the lock or someone else manually cleaned it up
				logger.error(`[Locker] Heartbeat failed: Lock lost for ${resource}`);
				this.releaseLock(resource, author, shardId);
			} else {
				lock.expiresAt = nextExpires;
			}
		} catch (e) {
			logger.error(`[Locker] Heartbeat failed for ${resource}`, e);
		}
	}

	public async releaseLock(resource: string, author: string, shardId: string = "main") {
		const lock = this.activeLocks.get(`${shardId}:${resource}`);
		if (lock) {
			clearInterval(lock.interval);
			this.activeLocks.delete(`${shardId}:${resource}`);
		}

		try {
			const db = await this.pool.getDb(shardId);
			await db.deleteFrom("claims" as any)
				.where("path", "=", resource)
				.where("author", "=", author)
				.execute();
		} catch (e) {
			logger.error(`[Locker] Release failed for ${resource}`, e);
		}
	}

	public destroy() {
		for (const lock of this.activeLocks.values()) {
			clearInterval(lock.interval);
		}
		this.activeLocks.clear();
	}
}
