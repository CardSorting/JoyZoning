import type { Schema } from "../Config.js";
import { isIncrement, normalizeWhere } from "./types.js";
import type { WhereCondition, WriteOp } from "./types.js";

/**
 * Level 7: Memory-First Query Engine.
 * Responsible for merging disk results with in-memory write buffers
 * to provide a consistent view of the world (Read-Your-Writes).
 */

/**
 * Simulates a table scan or index lookup over the provided operations buffer.
 */
export function applyOpsToResults<T extends keyof Schema>(
	table: T,
	opsMap: Map<keyof Schema, WriteOp[]> | undefined,
	shardIndexMapByTable: Map<keyof Schema, Map<string, Map<string, WriteOp>>> | undefined,
	target: Schema[T][],
	conditions: WhereCondition[],
	warmedIndices: Set<string>,
	shardId: string,
): void {
	const tableOps = opsMap?.get(table);
	if (!tableOps) return;

	// Level 7: Auth-Index Optimization (Fast Status Filter)
	const statusCond = conditions.find((c) => c.column === "status" && (c.operator === "=" || !c.operator));
	if (statusCond && typeof statusCond.value === "string") {
		const indexKey = `status:${statusCond.value}`;
		const isWarmed = warmedIndices.has(`${shardId}:${table as string}:${indexKey}`);

		const idMap = shardIndexMapByTable?.get(table)?.get(indexKey);
		if (idMap) {
			for (const op of idMap.values()) {
				applySingleOp(target, op, conditions);
			}
			if (isWarmed) {
				// Authoritative Index: If warmed, we don't need to scan the whole buffer for this status
				return;
			}
		}
	}

	// Fallback: Buffer Scan
	for (const op of tableOps) {
		applySingleOp(target, op, conditions);
	}
}

const REGEX_CACHE = new Map<string, RegExp>();
const MAX_REGEX_CACHE = 1000;

export function applySingleOp<T extends keyof Schema>(
	target: Schema[T][],
	op: WriteOp,
	conditions: WhereCondition[],
): void {
	const opWhere = normalizeWhere(op.where);

	const matchesOp = (row: Record<string, unknown>, conds: WhereCondition[]) => {
		return conds.every((c) => {
			const val = row[c.column];
			const opStr = (c.operator || "=").toUpperCase();
			if (opStr === "=" || opStr === "IS") return val === c.value;
			if (opStr === "IS NOT" || opStr === "!=") return val !== c.value;
			if (opStr === "IN") {
				if (c.value instanceof Set) return (c.value as Set<unknown>).has(val);
				if (Array.isArray(c.value)) {
					const s = new Set<unknown>(c.value);
					(c as { value: unknown }).value = s; // Optimization: Cache the set on the condition itself
					return s.has(val);
				}
				return val === c.value;
			}
			if (opStr === ">") return Number(val) > Number(c.value);
			if (opStr === "<") return Number(val) < Number(c.value);
			if (opStr === ">=") return Number(val) >= Number(c.value);
			if (opStr === "<=") return Number(val) <= Number(c.value);
			if (opStr === "LIKE" && typeof val === "string" && typeof c.value === "string") {
				let regex = REGEX_CACHE.get(c.value);
				if (!regex) {
					if (REGEX_CACHE.size > MAX_REGEX_CACHE) REGEX_CACHE.clear();
					regex = new RegExp(`^${c.value.replace(/%/g, ".*").replace(/_/g, ".")}$`, "i");
					REGEX_CACHE.set(c.value, regex);
				}
				return regex.test(val);
			}
			return false;
		});
	};

	const matchesFinal = (row: Record<string, unknown>) => matchesOp(row, conditions);

	if (op.type === "insert" && op.values) {
		const newRow = { ...op.values } as unknown as Schema[T];
		if (matchesFinal(newRow as unknown as Record<string, unknown>)) target.push(newRow);
	} else if (op.type === "upsert" && op.values) {
		const pkMatch = (r: Schema[T]) => {
			const row = r as unknown as Record<string, unknown>;
			if (op.conflictTarget) {
				const targets = Array.isArray(op.conflictTarget) ? op.conflictTarget : [op.conflictTarget];
				return targets.every(col => row[col] !== undefined && (op.values as Record<string, unknown>)[col] !== undefined && row[col] === (op.values as Record<string, unknown>)[col]);
			}
			if (opWhere.length > 0) return matchesOp(row, opWhere);
			return row.id !== undefined && (op.values as Record<string, unknown>).id !== undefined && row.id === (op.values as Record<string, unknown>).id;
		};
		const existingIdx = target.findIndex(pkMatch);
		if (existingIdx >= 0) {
			const next = applyValues(target[existingIdx], op.values as Record<string, unknown>, !!op.hasIncrements);
			if (matchesFinal(next as unknown as Record<string, unknown>)) target[existingIdx] = next;
			else target.splice(existingIdx, 1);
		} else {
			const newRow = { ...op.values } as unknown as Schema[T];
			if (matchesFinal(newRow as unknown as Record<string, unknown>)) target.push(newRow);
		}
	} else if (op.type === "update" && op.values) {
		for (let i = target.length - 1; i >= 0; i--) {
			if (matchesOp(target[i] as unknown as Record<string, unknown>, opWhere)) {
				const next = applyValues(target[i], op.values as Record<string, unknown>, !!op.hasIncrements);
				if (matchesFinal(next as unknown as Record<string, unknown>)) target[i] = next;
				else target.splice(i, 1);
			}
		}
	} else if (op.type === "delete") {
		for (let i = target.length - 1; i >= 0; i--) {
			if (matchesOp(target[i] as unknown as Record<string, unknown>, opWhere)) target.splice(i, 1);
		}
	}
}

function applyValues<T>(existing: T, values: Record<string, unknown>, hasIncrements: boolean): T {
	const next = { ...existing } as Record<string, unknown>;
	for (const [k, v] of Object.entries(values)) {
		if (hasIncrements && isIncrement(v)) {
			next[k] = (Number(next[k]) || 0) + v.value;
		} else {
			next[k] = v;
		}
	}
	return next as T;
}

/**
 * Sorts and slices results according to query options.
 */
export function postProcessResults<T>(
	results: T[],
	options?: { orderBy?: { column: string; direction: "asc" | "desc" }; limit?: number; offset?: number },
): T[] {
	let final = results;
	if (options?.orderBy) {
		const col = options.orderBy.column;
		const dir = options.orderBy.direction;
		final.sort((a, b) => {
			const valA = (a as Record<string, unknown>)[col];
			const valB = (b as Record<string, unknown>)[col];
			if (valA === undefined || valB === undefined || valA === null || valB === null) return 0;
			if (valA < valB) return dir === "asc" ? -1 : 1;
			if (valA > valB) return dir === "asc" ? 1 : -1;
			return 0;
		});
	}
	if (options?.offset) final = final.slice(options.offset);
	if (options?.limit) final = final.slice(0, options.limit);
	return final;
}
