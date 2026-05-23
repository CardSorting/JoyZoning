import type { Kysely } from "kysely";
import type { Schema } from "../Config.js";

export type DbLayer = "domain" | "infrastructure" | "ui" | "plumbing";

export type WhereCondition = {
	column: string;
	value: string | number | string[] | number[] | null;
	operator?:
		| "="
		| "<"
		| ">"
		| "<="
		| ">="
		| "!="
		| "IN"
		| "in"
		| "In"
		| "UNSAFE_IN"
		| "IS"
		| "IS NOT"
		| "LIKE";
};

export type Increment = { _type: "increment"; value: number };

export type WriteOp = {
	type: "insert" | "update" | "delete" | "upsert";
	table: keyof Schema;
	values?: Record<string, unknown | Increment>;
	where?: WhereCondition | WhereCondition[];
	conflictTarget?: string | string[]; // For upserts
	agentId?: string;
	shardId?: string; // Level 8: Shard Partitioning
	layer?: DbLayer;
	// Level 6: Pre-calculated Metadata
	hasIncrements?: boolean;
	dedupKey?: string;
};

export const LAYER_PRIORITY: Record<DbLayer, number> = {
	domain: 0,
	infrastructure: 1,
	ui: 2,
	plumbing: 3,
};

export function normalizeWhere(
	where: WhereCondition | WhereCondition[] | undefined,
): WhereCondition[] {
	if (!where) return [];
	return Array.isArray(where) ? where : [where];
}

export function isIncrement(value: unknown): value is Increment {
	return (
		typeof value === "object" &&
		value !== null &&
		"_type" in value &&
		(value as Increment)._type === "increment"
	);
}

export interface IBufferedDbPool {
	getDb(shardId?: string): Promise<Kysely<Schema>>;
	selectOne<T extends keyof Schema>(
		table: T,
		where: WhereCondition | WhereCondition[],
		agentId?: string,
		options?: { shardId?: string; limit?: number; offset?: number },
	): Promise<Schema[T] | null>;
	selectWhere<T extends keyof Schema>(
		table: T,
		where: WhereCondition | WhereCondition[],
		agentId?: string,
		options?: { orderBy?: { column: string; direction: "asc" | "desc" }; limit?: number; offset?: number; shardId?: string },
	): Promise<Schema[T][]>;
	push(op: WriteOp, agentId?: string, affectedFile?: string): Promise<void>;
	flush(): Promise<void>;
}
