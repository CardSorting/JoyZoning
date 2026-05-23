import { sql } from "kysely";
import { getActiveShards, getDb } from "./Config.js";
import { logger } from "../util/Logger.js";
import { dbPool } from "./pool/index.js";

interface PragmaResult {
	integrity_check?: string;
	page_count?: number;
	freelist_count?: number;
}

/**
 * IntegrityWorker provides autonomous data validation and self-healing for the Sovereign Swarm.
 * It periodically audits all database shards for corruption and logical inconsistencies.
 */
export class IntegrityWorker {
	private interval: NodeJS.Timeout | null = null;
	private isProcessing = false;

	constructor(private checkIntervalMs = 600000) {} // Default 10 minutes

	start() {
		if (this.interval) return;
		this.interval = setInterval(() => this.runAudit(), this.checkIntervalMs);
		// Initial run
		setTimeout(() => this.runAudit(), 5000);
	}

	stop() {
		if (this.interval) {
			clearInterval(this.interval);
			this.interval = null;
		}
	}

	async runAudit() {
		if (this.isProcessing) return;
		this.isProcessing = true;

		try {
			logger.info("Starting swarm-wide integrity audit...");

			const shards = getActiveShards();

			for (const shardId of shards) {
				await this.auditShard(shardId);
			}

			logger.info("Audit complete.");
		} catch (e) {
			logger.error("Audit failed", e);
		} finally {
			this.isProcessing = false;
		}
	}

	private async auditShard(shardId: string) {
		const db = await getDb(shardId);

		// 1. Physical Integrity (Hardened: Use quick_check for production performance)
		const integrityResult = await sql`PRAGMA quick_check;`.execute(db);
		const row = integrityResult.rows[0] as { quick_check?: string; integrity_check?: string } | undefined;
		const status = row?.quick_check || row?.integrity_check;

		if (status !== "ok") {
			logger.error(`CRITICAL: Shard ${shardId} corruption detected`, {
				status,
			});
		}

		// 2. Logical Consistency: Batched Orphan Detection (Level 10 Scalability)
		let offset = 0;
		const BATCH_SIZE = 1000;
		let totalProcessed = 0;

		while (true) {
			const danglingNodes = await dbPool.selectWhere("nodes", [], undefined, {
				shardId,
				limit: BATCH_SIZE,
				offset,
			});

			if (danglingNodes.length === 0) break;

			const nodeIds = new Set(danglingNodes.map((n) => n.id));
			const orphans = danglingNodes.filter(
				(n) => n.parentId && !nodeIds.has(n.parentId),
			);

			if (orphans.length > 0) {
				logger.warn(
					`Shard ${shardId}: Found ${orphans.length} orphaned nodes in batch ${offset}. Repairing...`,
				);
				for (const orphan of orphans) {
					await dbPool.push({
						type: "update",
						table: "nodes",
						values: {
							parentId: null,
							message: `[AUTO-REPAIRED] ${orphan.message}`,
						},
						where: { column: "id", value: orphan.id },
						shardId,
					});
				}
				await dbPool.flush();
			}

			totalProcessed += danglingNodes.length;
			offset += BATCH_SIZE;
			
			// Safety: Don't audit more than 100k nodes per interval to prevent I/O saturation
			if (totalProcessed >= 100000) {
				logger.info(`Shard ${shardId}: Reached audit safety limit (100k nodes). Continuing in next cycle.`);
				break;
			}
		}

		// 3. Knowledge Edge Integrity (Level 10 Relational Hardening)
		try {
			const edges = await dbPool.selectWhere("knowledge_edges", [], undefined, { shardId, limit: 1000 });
			if (edges.length > 0) {
				// Verify sources exist in 'knowledge'
				// This is a sampling check for performance
				const sourceIds = [...new Set(edges.map(e => e.sourceId).filter((id): id is string => !!id))];
				const validSources = await dbPool.selectWhere("knowledge", [{ column: "id", value: sourceIds, operator: "IN" }], undefined, { shardId });
				const validSourceSet = new Set(validSources.map(s => s.id));
				
				const orphans = edges.filter(e => e.sourceId && !validSourceSet.has(e.sourceId));
				if (orphans.length > 0) {
					logger.warn(`Shard ${shardId}: Found ${orphans.length} dangling knowledge edges. Deleting...`);
					for (const orphan of orphans) {
						if (!orphan.sourceId || !orphan.targetId) continue;
						await dbPool.push({
							type: "delete",
							table: "knowledge_edges",
							where: [
								{ column: "sourceId", value: orphan.sourceId },
								{ column: "targetId", value: orphan.targetId }
							],
							shardId
						});
					}
					await dbPool.flush();
				}
			}
		} catch (e) {
			logger.error(`Shard ${shardId}: Knowledge edge audit failed`, e);
		}

		// 4. Telemetry Pruning (Self-Healing Storage)
		const weekAgo = Date.now() - 7 * 24 * 60 * 60 * 1000;
		await dbPool.push({
			type: "delete",
			table: "telemetry",
			where: { column: "timestamp", value: weekAgo, operator: "<" },
			shardId,
		});

		// 4. Maintenance: Fragmentation Check & Index Rebuild
		const pageCount = await sql`PRAGMA page_count;`.execute(db);
		const freelistCount = await sql`PRAGMA freelist_count;`.execute(db);

		const pCountRow = pageCount.rows[0] as PragmaResult | undefined;
		const fCountRow = freelistCount.rows[0] as PragmaResult | undefined;

		const pCount = Number(pCountRow?.page_count || 0);
		const fCount = Number(fCountRow?.freelist_count || 0);

		if (pCount > 1000 && fCount / pCount > 0.3) {
			logger.info(
				`Shard ${shardId}: Fragmentation high (${((fCount / pCount) * 100).toFixed(1)}%). Rebuilding indices...`,
			);
			await sql`REINDEX;`.execute(db);
			await sql`VACUUM;`.execute(db);
		}
	}
}

export const integrityWorker = new IntegrityWorker();
