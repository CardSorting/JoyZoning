import * as fs from "node:fs";
import * as path from "node:path";
import { CompiledQuery, Kysely, SqliteDialect } from "kysely";
import type { Schema } from "./DatabaseSchema.js";
export type { Schema };

const isBun = !!(globalThis as { Bun?: unknown }).Bun;


const _dbs = new Map<string, Kysely<Schema>>();
const _rawDbs = new Map<string, unknown>();
const _initPromises = new Map<string, Promise<Kysely<Schema>>>();
let _dbPath: string | null = null;
const _isInitialized = new Set<string>();

export function setDbPath(dbPath: string) {
	_dbPath = dbPath;
}

export async function getDb(shardId: string = "main"): Promise<Kysely<Schema>> {
	const existing = _dbs.get(shardId);
	if (existing) return existing;

	// Level 10: Atomic Initialization Lock (Prevents Schema Race Conditions)
	let initPromise = _initPromises.get(shardId);
	if (!initPromise) {
		initPromise = (async () => {
			if (!_dbPath) {
				_dbPath = path.resolve(process.cwd(), "broccoliq.db");
			}

			const dbDir = path.dirname(_dbPath);
			if (!fs.existsSync(dbDir)) fs.mkdirSync(dbDir, { recursive: true });

			const shardPath =
				shardId === "main"
					? _dbPath
					: path.join(dbDir, `${path.basename(_dbPath, ".db")}_${shardId}.db`);

			let dialect: import("kysely").Dialect;
			let rawDb: unknown;

			if (isBun) {
				// Native Bun Support: O(1) N-API Overhead reduction
				// @ts-ignore
				const { Database } = await import("bun:sqlite");
				// @ts-ignore
				const { BunSqliteDialect } = await import("kysely-bun-sqlite");
				rawDb = new Database(shardPath);
				dialect = new BunSqliteDialect({
					database: rawDb,
				});
			} else {
				// Production-grade Node Support
				// biome-ignore lint/suspicious/noExplicitAny: Dynamic import requires any
				const Database = (await import("better-sqlite3") as any).default;
				rawDb = new Database(shardPath);
				dialect = new SqliteDialect({
					// biome-ignore lint/suspicious/noExplicitAny: Kysely database type mismatch
					database: rawDb as any,
				});
			}

			const db = new Kysely<Schema>({
				dialect,
			});

			_rawDbs.set(shardId, rawDb);
			_dbs.set(shardId, db);

			if (!_isInitialized.has(shardId)) {
				await initializeSchema(db);
				_isInitialized.add(shardId);
			}

			return db;
		})();
		_initPromises.set(shardId, initPromise);
	}

	return initPromise;
}

export async function getRawDb(
	shardId: string = "main",
): Promise<unknown> {
	const existing = _rawDbs.get(shardId);
	if (existing) return existing;

	// Initialize if missing
	await getDb(shardId);
	const initialized = _rawDbs.get(shardId);
	if (!initialized)
		throw new Error(`Failed to initialize raw DB for shard: ${shardId}`);
	return initialized;
}

export function getActiveShards(): string[] {
	return Array.from(_dbs.keys());
}

/**
 * Level 11: Atomic Termination Engine.
 * Physically closes all SQLite connection handles to ensure zero resource leakage.
 */
export async function closeAllShards() {
	for (const [shardId, db] of _dbs.entries()) {
		try {
			// Ensure WAL checkpointing before hard closure if possible
			// (Though BufferedDbPool should have done it already)
			await db.destroy();
			_dbs.delete(shardId);
			_rawDbs.delete(shardId);
			_initPromises.delete(shardId);
			_isInitialized.delete(shardId);
		} catch (e) {
			console.error(`[Config] Failed to close shard ${shardId}:`, e);
		}
	}
}

async function applyPragmas(db: Kysely<Schema>) {
	const execute = (q: string) => db.executeQuery(CompiledQuery.raw(q));
	await execute("PRAGMA journal_mode = WAL;");
	await execute("PRAGMA synchronous = NORMAL;");
	await execute("PRAGMA foreign_keys = ON;");
	await execute("PRAGMA cache_size = -128000;");
	await execute("PRAGMA temp_store = MEMORY;");
	await execute("PRAGMA mmap_size = 2147483648;");
	await execute("PRAGMA threads = 4;");
}

async function ensureColumn(db: Kysely<Schema>, table: string, column: string, definition: string) {
	try {
		const info = await db.executeQuery(CompiledQuery.raw(`PRAGMA table_info(${table})`));
		const exists = info.rows.some((row) => (row as { name: string }).name === column);
		if (!exists) {
			console.warn(`[DbPool] 🛡️ Self-Healing: Missing column detected. Adding '${column}' to '${table}'...`);
			await db.executeQuery(CompiledQuery.raw(`ALTER TABLE ${table} ADD COLUMN ${column} ${definition}`));
			
			// Verify again for peace of mind
			const verify = await db.executeQuery(CompiledQuery.raw(`PRAGMA table_info(${table})`));
			const nowExists = verify.rows.some((row: any) => row.name === column);
			if (nowExists) {
				console.log(`[DbPool] ✅ Column '${column}' successfully injected into '${table}'.`);
			} else {
				throw new Error(`Column injection verification failed for ${table}.${column}`);
			}
		}
	} catch (e) {
		console.error(`[DbPool] ❌ Critical Self-Healing failure for ${table}.${column}:`, e);
	}
}

async function initializeSchema(db: Kysely<Schema>) {
	await applyPragmas(db);
	const execute = (q: string) => db.executeQuery(CompiledQuery.raw(q));

	// Level 2: Self-Healing Column Migration (Legacy Support)
	try {
		for (const table of ["settings", "queue_settings"]) {
			const info = await db.executeQuery(CompiledQuery.raw(`PRAGMA table_info(${table})`));
			const hasName = info.rows.some((row) => (row as { name: string }).name === "name");
			const hasKey = info.rows.some((row) => (row as { name: string }).name === "key");
			if (hasName && !hasKey) {
				console.warn(`[DbPool] 🛡️ Self-Healing: Migrating column 'name' to 'key' in '${table}'...`);
				await db.executeQuery(CompiledQuery.raw(`ALTER TABLE ${table} RENAME COLUMN name TO key`));
				console.log(`[DbPool] ✅ Column 'name' successfully renamed to 'key' in '${table}'.`);
			}
		}
	} catch (e) {
		console.warn("[DbPool] Self-healing migration skipped or failed:", e);
	}

	// Schema Initialization

	await execute(`CREATE TABLE IF NOT EXISTS users (
    id TEXT PRIMARY KEY,
    createdAt BIGINT
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS workspaces (
    id TEXT PRIMARY KEY,
    userId TEXT NOT NULL,
    sharedMemoryLayer TEXT,
    createdAt BIGINT,
    FOREIGN KEY(userId) REFERENCES users(id)
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS repositories (
    id TEXT PRIMARY KEY,
    workspaceId TEXT NOT NULL,
    repoId TEXT NOT NULL,
    repoPath TEXT NOT NULL,
    forkedFrom TEXT,
    forkedFromRemote TEXT,
    defaultBranch TEXT NOT NULL,
    createdAt BIGINT,
    FOREIGN KEY(workspaceId) REFERENCES workspaces(id)
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS branches (
    repoPath TEXT NOT NULL,
    name TEXT NOT NULL,
    head TEXT NOT NULL,
    isEphemeral INTEGER DEFAULT 0,
    createdAt BIGINT,
    expiresAt BIGINT,
    PRIMARY KEY(repoPath, name)
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS tags (
    repoPath TEXT NOT NULL,
    name TEXT NOT NULL,
    head TEXT NOT NULL,
    createdAt BIGINT,
    PRIMARY KEY(repoPath, name)
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS nodes (
    id TEXT PRIMARY KEY,
    repoPath TEXT NOT NULL,
    parentId TEXT,
    data TEXT,
    message TEXT,
    timestamp BIGINT,
    author TEXT,
    type TEXT,
    tree TEXT,
    usage TEXT,
    metadata TEXT
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS trees (
    id TEXT PRIMARY KEY,
    repoPath TEXT NOT NULL,
    entries TEXT NOT NULL,
    createdAt BIGINT
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS files (
    id TEXT PRIMARY KEY,
    path TEXT NOT NULL,
    content TEXT NOT NULL,
    encoding TEXT NOT NULL,
    size INTEGER NOT NULL,
    updatedAt BIGINT NOT NULL,
    author TEXT NOT NULL
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS reflog (
    id TEXT PRIMARY KEY,
    repoPath TEXT NOT NULL,
    ref TEXT NOT NULL,
    oldHead TEXT,
    newHead TEXT NOT NULL,
    author TEXT NOT NULL,
    message TEXT NOT NULL,
    timestamp BIGINT NOT NULL,
    operation TEXT NOT NULL
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS stashes (
    id TEXT PRIMARY KEY,
    repoPath TEXT NOT NULL,
    branch TEXT NOT NULL,
    nodeId TEXT NOT NULL,
    data TEXT NOT NULL,
    tree TEXT NOT NULL,
    label TEXT NOT NULL,
    createdAt BIGINT NOT NULL
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS claims (
    path TEXT PRIMARY KEY,
    repoPath TEXT NOT NULL,
    branch TEXT NOT NULL,
    author TEXT NOT NULL,
    timestamp BIGINT NOT NULL,
    expiresAt BIGINT NOT NULL
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS telemetry (
    id TEXT PRIMARY KEY,
    repoPath TEXT NOT NULL,
    agentId TEXT NOT NULL,
    taskId TEXT,
    promptTokens INTEGER NOT NULL,
    completionTokens INTEGER NOT NULL,
    totalTokens INTEGER NOT NULL,
    modelId TEXT NOT NULL,
    cost REAL NOT NULL,
    timestamp BIGINT NOT NULL,
    environment TEXT NOT NULL
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS telemetry_aggregates (
    id TEXT PRIMARY KEY,
    repoPath TEXT NOT NULL,
    totalCommits INTEGER DEFAULT 0,
    totalTokens INTEGER DEFAULT 0,
    totalCost REAL DEFAULT 0
  )`);

	// Indices
	await execute(`CREATE INDEX IF NOT EXISTS idx_nodes_repo ON nodes(repoPath)`);
	await execute(`CREATE INDEX IF NOT EXISTS idx_nodes_parent ON nodes(parentId)`);
	await execute(`CREATE INDEX IF NOT EXISTS idx_nodes_repo_parent ON nodes(repoPath, parentId)`);
	await execute(
		`CREATE INDEX IF NOT EXISTS idx_branches_repo ON branches(repoPath)`,
	);
	await execute(
		`CREATE INDEX IF NOT EXISTS idx_telemetry_repo ON telemetry(repoPath)`,
	);
	await execute(
		`CREATE INDEX IF NOT EXISTS idx_telemetry_task ON telemetry(taskId)`,
	);

	await execute(`CREATE TABLE IF NOT EXISTS agents (
    id TEXT PRIMARY KEY,
    userId TEXT NOT NULL,
    name TEXT NOT NULL,
    role TEXT NOT NULL,
    permissions TEXT,
    memoryLayer TEXT,
    createdAt BIGINT,
    lastActive BIGINT,
    FOREIGN KEY(userId) REFERENCES users(id)
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS knowledge (
    id TEXT PRIMARY KEY,
    userId TEXT NOT NULL,
    type TEXT NOT NULL,
    content TEXT NOT NULL,
    tags TEXT,
    edges TEXT,
    inboundEdges TEXT,
    embedding TEXT,
    confidence REAL,
    hubScore INTEGER,
    expiresAt BIGINT,
    metadata TEXT,
    createdAt BIGINT,
    FOREIGN KEY(userId) REFERENCES users(id)
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS tasks (
    id TEXT PRIMARY KEY,
    userId TEXT NOT NULL,
    agentId TEXT NOT NULL,
    status TEXT NOT NULL,
    description TEXT NOT NULL,
    complexity REAL,
    linkedKnowledgeIds TEXT,
    result TEXT,
    createdAt BIGINT,
    updatedAt BIGINT,
    FOREIGN KEY(userId) REFERENCES users(id),
    FOREIGN KEY(agentId) REFERENCES agents(id)
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS audit_events (
    id TEXT PRIMARY KEY,
    userId TEXT NOT NULL,
    agentId TEXT,
    type TEXT NOT NULL,
    data TEXT NOT NULL,
    createdAt BIGINT NOT NULL,
    FOREIGN KEY(userId) REFERENCES users(id)
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS settings (
    id TEXT PRIMARY KEY,
    key TEXT NOT NULL UNIQUE,
    value TEXT NOT NULL,
    updatedAt BIGINT NOT NULL
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS logical_constraints (
    id TEXT PRIMARY KEY,
    repoPath TEXT NOT NULL,
    pathPattern TEXT NOT NULL,
    knowledgeId TEXT NOT NULL,
    severity TEXT NOT NULL,
    createdAt BIGINT NOT NULL,
    FOREIGN KEY(knowledgeId) REFERENCES knowledge(id)
  )`);

	await execute(
		`CREATE INDEX IF NOT EXISTS idx_logical_repo ON logical_constraints(repoPath)`,
	);
	await execute(
		`CREATE INDEX IF NOT EXISTS idx_logical_pattern ON logical_constraints(pathPattern)`,
	);

	await execute(`CREATE TABLE IF NOT EXISTS knowledge_edges (
    id TEXT PRIMARY KEY,
    sourceId TEXT NOT NULL,
    targetId TEXT NOT NULL,
    type TEXT NOT NULL,
    weight REAL DEFAULT 1.0,
    FOREIGN KEY(sourceId) REFERENCES knowledge(id),
    FOREIGN KEY(targetId) REFERENCES knowledge(id)
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS decisions (
    id TEXT PRIMARY KEY,
    repoPath TEXT NOT NULL,
    agentId TEXT NOT NULL,
    taskId TEXT,
    decision TEXT NOT NULL,
    rationale TEXT NOT NULL,
    knowledgeIds TEXT NOT NULL,
    timestamp BIGINT NOT NULL,
    FOREIGN KEY(agentId) REFERENCES agents(id)
  )`);

	await execute(
		`CREATE INDEX IF NOT EXISTS idx_edges_source ON knowledge_edges(sourceId)`,
	);
	await execute(
		`CREATE INDEX IF NOT EXISTS idx_edges_target ON knowledge_edges(targetId)`,
	);
	await execute(
		`CREATE INDEX IF NOT EXISTS idx_decisions_repo ON decisions(repoPath)`,
	);
	await execute(
		`CREATE INDEX IF NOT EXISTS idx_decisions_task ON decisions(taskId)`,
	);

	// Queue Tables
	await execute(`CREATE TABLE IF NOT EXISTS queue_jobs (
    id TEXT PRIMARY KEY,
    payload TEXT NOT NULL,
    status TEXT NOT NULL,
    priority INTEGER DEFAULT 0,
    attempts INTEGER DEFAULT 0,
    maxAttempts INTEGER DEFAULT 5,
    runAt BIGINT,
    error TEXT,
    createdAt BIGINT,
    updatedAt BIGINT
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS queue_settings (
    id TEXT PRIMARY KEY,
    key TEXT NOT NULL UNIQUE,
    value TEXT NOT NULL,
    updatedAt BIGINT NOT NULL
  )`);

	// Level 2: Sovereing Hive Tables (DietCode integration)
	await execute(`CREATE TABLE IF NOT EXISTS hive_kb (
    id TEXT PRIMARY KEY,
    knowledge_key TEXT NOT NULL,
    knowledge_value TEXT NOT NULL,
    type TEXT NOT NULL,
    confidence REAL NOT NULL,
    tags TEXT NOT NULL,
    metadata TEXT,
    created_at TEXT NOT NULL
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS hive_healing_proposals (
    id TEXT PRIMARY KEY,
    violation_id TEXT NOT NULL,
    violation TEXT NOT NULL,
    rationale TEXT NOT NULL,
    proposed_code TEXT NOT NULL,
    status TEXT NOT NULL,
    confidence REAL DEFAULT 1.0,
    created_at TEXT NOT NULL,
    applied_at TEXT
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS hive_snapshots (
    id TEXT PRIMARY KEY,
    path TEXT NOT NULL,
    content TEXT NOT NULL,
    timestamp INTEGER NOT NULL,
    hash TEXT NOT NULL,
    mtime INTEGER
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS hive_file_context (
    id TEXT PRIMARY KEY,
    path TEXT NOT NULL UNIQUE,
    state TEXT NOT NULL,
    source TEXT NOT NULL,
    last_read_date INTEGER,
    last_edit_date INTEGER,
    signature TEXT,
    external_edit_detected INTEGER DEFAULT 0
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS hive_audit (
    id TEXT PRIMARY KEY,
    session_id TEXT,
    user_id TEXT,
    agent_id TEXT,
    type TEXT NOT NULL,
    message TEXT NOT NULL,
    data TEXT,
    timestamp INTEGER NOT NULL
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS hive_agent_sessions (
    id TEXT PRIMARY KEY,
    agent_id TEXT NOT NULL,
    status TEXT NOT NULL,
    start_time INTEGER NOT NULL,
    end_time INTEGER
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS hive_joy_imports (
    id TEXT PRIMARY KEY,
    source_path TEXT NOT NULL,
    imported_path TEXT NOT NULL
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS hive_joy_history (
    id TEXT PRIMARY KEY,
    violation_count INTEGER NOT NULL,
    file_count INTEGER NOT NULL,
    timestamp INTEGER NOT NULL
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS hive_metabolic_telemetry (
    id TEXT PRIMARY KEY,
    task_id TEXT,
    reads INTEGER NOT NULL,
    writes INTEGER NOT NULL,
    lines_added INTEGER NOT NULL,
    lines_deleted INTEGER NOT NULL,
    tokens_processed INTEGER NOT NULL,
    verifications_success INTEGER NOT NULL,
    timestamp INTEGER NOT NULL
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS hive_tasks (
    id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL UNIQUE,
    user_id TEXT,
    agent_id TEXT,
    title TEXT NOT NULL,
    objective TEXT NOT NULL,
    description TEXT,
    status TEXT NOT NULL,
    priority INTEGER NOT NULL,
    vitals_heartbeat TEXT,
    v_token TEXT,
    initial_context TEXT,
    result TEXT,
    created_at INTEGER NOT NULL,
    updated_at INTEGER NOT NULL,
    started_at INTEGER,
    completed_at INTEGER,
    user_agent TEXT NOT NULL
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS hive_llm_telemetry (
    id TEXT PRIMARY KEY,
    repo_path TEXT NOT NULL,
    agent_id TEXT NOT NULL,
    task_id TEXT,
    prompt_tokens INTEGER,
    completion_tokens INTEGER,
    total_tokens INTEGER,
    model_id TEXT,
    cost REAL,
    timestamp INTEGER NOT NULL,
    environment TEXT
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS hive_joy_metrics (
    id TEXT PRIMARY KEY,
    path TEXT NOT NULL,
    violation_count INTEGER NOT NULL,
    hash TEXT NOT NULL,
    last_scanned INTEGER NOT NULL
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS hive_joy_bypasses (
    id TEXT PRIMARY KEY,
    path TEXT NOT NULL UNIQUE,
    violation_type TEXT NOT NULL,
    timestamp INTEGER NOT NULL
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS hive_locks (
    id TEXT PRIMARY KEY,
    resource TEXT NOT NULL,
    owner_id TEXT NOT NULL,
    lock_code TEXT NOT NULL,
    expires_at INTEGER NOT NULL,
    acquired_at INTEGER NOT NULL
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS hive_queue (
    id TEXT PRIMARY KEY,
    type TEXT NOT NULL,
    status TEXT NOT NULL,
    total_shards INTEGER NOT NULL,
    completed_shards INTEGER DEFAULT 0,
    metadata TEXT,
    created_at INTEGER NOT NULL,
    updated_at INTEGER NOT NULL
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS hive_job_results (
    id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    shard_id INTEGER NOT NULL,
    status TEXT NOT NULL,
    payload TEXT,
    error TEXT,
    priority INTEGER DEFAULT 0,
    timestamp INTEGER NOT NULL
  )`);

	await execute(`CREATE TABLE IF NOT EXISTS hive_scoring_cache (
    id TEXT PRIMARY KEY,
    hash TEXT NOT NULL UNIQUE,
    result TEXT NOT NULL,
    timestamp INTEGER NOT NULL
  )`);

	// Level 2: Self-Healing Column Injection (Legacy Support)
	// Note: We use TEXT instead of TEXT PRIMARY KEY here because SQLite 
	// does not support adding PRIMARY KEY columns via ALTER TABLE.
	await ensureColumn(db, "users", "id", "TEXT");
	await ensureColumn(db, "workspaces", "id", "TEXT");
	await ensureColumn(db, "repositories", "id", "TEXT");
	await ensureColumn(db, "branches", "id", "TEXT");
	await ensureColumn(db, "tags", "id", "TEXT");
	await ensureColumn(db, "nodes", "id", "TEXT");
	await ensureColumn(db, "trees", "id", "TEXT");
	await ensureColumn(db, "files", "id", "TEXT");
	await ensureColumn(db, "reflog", "id", "TEXT");
	await ensureColumn(db, "stashes", "id", "TEXT");
	await ensureColumn(db, "claims", "id", "TEXT");
	await ensureColumn(db, "telemetry", "id", "TEXT");
	await ensureColumn(db, "telemetry_aggregates", "id", "TEXT");
	await ensureColumn(db, "agents", "id", "TEXT");
	await ensureColumn(db, "knowledge", "id", "TEXT");
	await ensureColumn(db, "tasks", "id", "TEXT");
	await ensureColumn(db, "audit_events", "id", "TEXT");
	await ensureColumn(db, "settings", "id", "TEXT");
	await ensureColumn(db, "logical_constraints", "id", "TEXT");
	await ensureColumn(db, "knowledge_edges", "id", "TEXT");
	await ensureColumn(db, "decisions", "id", "TEXT");
	await ensureColumn(db, "queue_jobs", "id", "TEXT");
	await ensureColumn(db, "queue_settings", "id", "TEXT");

	// Self-Healing for Hive Tables
	await ensureColumn(db, "hive_kb", "id", "TEXT");
	await ensureColumn(db, "hive_healing_proposals", "id", "TEXT");
	await ensureColumn(db, "hive_snapshots", "id", "TEXT");
	await ensureColumn(db, "hive_file_context", "id", "TEXT");
	await ensureColumn(db, "hive_audit", "id", "TEXT");
	await ensureColumn(db, "hive_agent_sessions", "id", "TEXT");
	await ensureColumn(db, "hive_joy_imports", "id", "TEXT");
	await ensureColumn(db, "hive_joy_history", "id", "TEXT");
	await ensureColumn(db, "hive_metabolic_telemetry", "id", "TEXT");
	await ensureColumn(db, "hive_tasks", "id", "TEXT");
	await ensureColumn(db, "hive_llm_telemetry", "id", "TEXT");
	await ensureColumn(db, "hive_joy_metrics", "id", "TEXT");
	await ensureColumn(db, "hive_joy_bypasses", "id", "TEXT");
	await ensureColumn(db, "hive_locks", "id", "TEXT");
	await ensureColumn(db, "hive_queue", "id", "TEXT");
	await ensureColumn(db, "hive_job_results", "id", "TEXT");

	// Hive Indices
	await execute(`CREATE INDEX IF NOT EXISTS idx_hive_telemetry_task ON hive_llm_telemetry (task_id)`);
	await execute(`CREATE INDEX IF NOT EXISTS idx_hive_metabolic_task ON hive_metabolic_telemetry (task_id)`);
	await execute(`CREATE INDEX IF NOT EXISTS idx_hive_kb_key ON hive_kb (knowledge_key)`);

	await execute(`CREATE INDEX IF NOT EXISTS idx_agents_user ON agents(userId)`);
	await execute(
		`CREATE INDEX IF NOT EXISTS idx_knowledge_user ON knowledge(userId)`,
	);
	await execute(`CREATE INDEX IF NOT EXISTS idx_tasks_user ON tasks(userId)`);
	await execute(`CREATE INDEX IF NOT EXISTS idx_tasks_agent ON tasks(agentId)`);
	await execute(
		`CREATE INDEX IF NOT EXISTS idx_audit_type ON audit_events(type)`,
	);
	await execute(
		`CREATE INDEX IF NOT EXISTS idx_audit_agent ON audit_events(agentId)`,
	);

	console.log("[DbPool] 🏛️ Sovereign Hive Schema baseline established and hardened.");
	return db;
}
