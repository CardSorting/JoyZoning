import { sql, type Transaction } from "kysely";
import { logger } from "../../util/Logger.js";
import type { Schema } from "../Config.js";
import { isIncrement, normalizeWhere, type WriteOp } from "./types.js";

interface RawDatabase {
	prepare(sql: string): {
		run(...params: unknown[]): void;
	};
}

/**
 * Level 3: Runtime Operations Engine.
 * Handles the low-level execution of write operations against SQLite.
 */

/**
 * Coalesces updates for the same ID to minimize write amplification.
 */
export function groupOps(ops: WriteOp[]): WriteOp[][] {
	const coalescedOps: WriteOp[] = [];
	const updateCache = new Map<string, number>();

	for (const op of ops) {
		if (op.type === "update" && op.dedupKey) {
			const existingIdx = updateCache.get(op.dedupKey);
			if (existingIdx !== undefined) {
				const targetOp = coalescedOps[existingIdx];
				if (targetOp?.values && op.values) {
					for (const [key, val] of Object.entries(op.values)) {
						const existingVal = targetOp.values[key];
						if (isIncrement(val)) {
							if (isIncrement(existingVal)) {
								existingVal.value += val.value;
							} else if (typeof existingVal === "number") {
								targetOp.values[key] = existingVal + val.value;
							} else {
								targetOp.values[key] = { ...val };
							}
						} else {
							targetOp.values[key] = val;
						}
					}
					continue;
				}
			} else {
				updateCache.set(op.dedupKey, coalescedOps.length);
			}
		}
		coalescedOps.push(op);
	}

	// Simple grouping of sequential inserts
	const groups: WriteOp[][] = [];
	let currentGroup: WriteOp[] = [];
	for (const op of coalescedOps) {
		if (op.type === "insert" && op.values) {
			if (currentGroup.length > 0 && currentGroup[0]?.table === op.table && currentGroup[0]?.type === "insert") {
				currentGroup.push(op);
			} else {
				if (currentGroup.length > 0) groups.push(currentGroup);
				currentGroup = [op];
			}
		} else {
			if (currentGroup.length > 0) groups.push(currentGroup);
			currentGroup = [];
			groups.push([op]);
		}
	}
	if (currentGroup.length > 0) groups.push(currentGroup);
	return groups;
}

/**
 * High-Performance Path: Zero-allocation raw SQL insert chunking.
 */
export async function executeChunkedRawInsert(
	table: keyof Schema,
	group: WriteOp[],
	rawDb: RawDatabase,
	parameterBuffer: unknown[],
): Promise<number> {
	if (group.length === 0) return 0;
	const firstOp = group[0];
	if (!firstOp?.values) return 0;

	const columns = Object.keys(firstOp.values);
	const CHUNK_SIZE = 100;
	let totalFlushed = 0;

	const isUpsert = firstOp.type === "upsert";
	let conflictClause = "";
	if (isUpsert) {
		let target = "";
		if (firstOp.conflictTarget) {
			target = Array.isArray(firstOp.conflictTarget) 
				? firstOp.conflictTarget.map(t => `"${t}"`).join(",") 
				: `"${firstOp.conflictTarget}"`;
		} else {
			// Hardened Logic: Detect composite PK tables
			if (table === "branches" || table === "tags") {
				target = '"repoPath","name"';
			} else if (["settings", "queue_settings"].includes(table)) {
				target = '"key"';
			} else {
				target = '"id"';
			}
		}
		const updates = columns.map(col => `"${col}"=excluded."${col}"`).join(",");
		conflictClause = ` ON CONFLICT(${target}) DO UPDATE SET ${updates}`;
	}

	for (let i = 0; i < group.length; i += CHUNK_SIZE) {
		const chunk = group.slice(i, i + CHUNK_SIZE);
		const placeholders = chunk.map(() => `(${columns.map(() => "?").join(",")})`).join(",");
		const sqlStr = `INSERT INTO "${table as string}" (${columns.map(c => `"${c}"`).join(",")}) VALUES ${placeholders}${conflictClause}`;

		let pIdx = 0;
		for (const op of chunk) {
			const vals = op.values as Record<string, unknown>;
			for (const col of columns) {
				parameterBuffer[pIdx++] = vals[col];
			}
		}

		const stmt = rawDb.prepare(sqlStr);
		stmt.run(...parameterBuffer.slice(0, pIdx));
		totalFlushed += chunk.length;
	}

	return totalFlushed;
}

export async function executeBulkInsert(trx: Transaction<Schema>, table: keyof Schema, group: WriteOp[]): Promise<number> {
	const firstOp = group[0];
	if (!firstOp?.values) return 0;
	const columnCount = Object.keys(firstOp.values).length || 1;
	const CHUNK_SIZE = Math.max(1, Math.floor(5000 / columnCount));
	let flushed = 0;
	
	const isUpsert = firstOp.type === "upsert";
	let conflictTarget = firstOp.conflictTarget;
	if (!conflictTarget) {
		if (table === "branches" || table === "tags") {
			conflictTarget = ["repoPath", "name"];
		} else if (["settings", "queue_settings"].includes(table)) {
			conflictTarget = "key";
		} else {
			conflictTarget = "id";
		}
	}

	for (let i = 0; i < group.length; i += CHUNK_SIZE) {
		const chunk = group.slice(i, i + CHUNK_SIZE);
		const values = chunk.map((op) => op.values).filter((v): v is Record<string, unknown> => v !== undefined);
		
		let query = trx.insertInto(table).values(values as never);
		if (isUpsert) {
			query = query.onConflict((oc) => {
				let builder = oc;
				if (Array.isArray(conflictTarget)) builder = builder.columns(conflictTarget as never);
				else builder = builder.column(conflictTarget as never);
				return builder.doUpdateSet(values[0] as never);
			}) as any;
		}
		
		await query.execute();
		flushed += chunk.length;
	}
	return flushed;
}

export async function executeSingleOp(trx: Transaction<Schema>, op: WriteOp) {
	const conditions = normalizeWhere(op.where);
	if (op.type === "insert" && op.values) {
		logger.debug(`[DbPool] ↳ 📝 INSERT INTO ${op.table} values: ${JSON.stringify(Object.keys(op.values))}`);
		await trx.insertInto(op.table).values(op.values as never).execute();
	} else if (op.type === "upsert" && op.values) {
		logger.debug(`[DbPool] ↳ 🔄 UPSERT INTO ${op.table} values: ${JSON.stringify(Object.keys(op.values))}`);
		// Modern Architecture: Support flexible conflict targets (defaulting to 'id')
		const query = trx.insertInto(op.table).values(op.values as never).onConflict((oc) => {
			let builder = oc;
			if (Array.isArray(op.conflictTarget)) {
				builder = builder.columns(op.conflictTarget as never);
			} else if (op.conflictTarget) {
				builder = builder.column(op.conflictTarget as never);
			} else {
				// Hardened Logic: Detect composite PK tables
				if (op.table === "branches" || op.table === "tags") {
					builder = builder.columns(["repoPath", "name"] as never);
				} else if (["settings", "queue_settings"].includes(op.table)) {
					builder = builder.column("key" as never);
				} else {
					builder = builder.column("id" as never);
				}
			}
			return builder.doUpdateSet(op.values as never);
		});
		await query.execute();
	} else if (op.type === "update" && op.values) {
		const sets: Record<string, unknown> = {};
		for (const [k, v] of Object.entries(op.values)) {
			if (isIncrement(v)) sets[k] = sql`${sql.ref(k)} + ${v.value}`;
			else sets[k] = v;
		}
		let query = trx.updateTable(op.table).set(sets as never) as any;
		for (const cond of conditions) {
			let opStr: string = (cond.operator || "=").toLowerCase();
			if (opStr === "in") opStr = "in"; // Ensure lowercase for Kysely
			query = query.where(cond.column as never, opStr as any, cond.value as never);
		}
		await query.execute();
	} else if (op.type === "delete") {
		let query = trx.deleteFrom(op.table) as any;
		for (const cond of conditions) {
			const opStr: string = (cond.operator || "=").toLowerCase();
			query = query.where(cond.column as never, opStr as any, cond.value as never);
		}
		await query.execute();
	}
}
