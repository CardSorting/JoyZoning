/**
 * [LAYER: INFRASTRUCTURE]
 * [SUB-ZONE: database]
 * Principle: Unified TypeScript type definitions for Kysely database schema
 * Hardening: Master Sovereign Hive Architecture (v2.0)
 * 
 * Provides bit-for-bit parity across DietCode and BroccoliQ/broccolidb.
 */

export interface Schema {
	// --- CORE BROCCOLIQ TABLES ---
	users: {
		id: string;
		createdAt: number;
	};
	workspaces: {
		id: string;
		userId: string;
		sharedMemoryLayer: string; // JSON array string
		createdAt: number;
	};
	repositories: {
		id: string;
		workspaceId: string;
		repoId: string;
		repoPath: string;
		forkedFrom?: string;
		forkedFromRemote?: string;
		defaultBranch: string;
		createdAt: number;
	};
	branches: {
		repoPath: string; // Composite key part: {repoPath}/{name}
		name: string;
		head: string;
		isEphemeral: number; // boolean as 0/1
		createdAt: number;
		expiresAt: number | null;
	};
	tags: {
		repoPath: string;
		name: string;
		head: string;
		createdAt: number;
	};
	nodes: {
		id: string;
		repoPath: string;
		parentId: string | null;
		data: string; // JSON string
		message: string;
		timestamp: number;
		author: string;
		type: "snapshot" | "summary" | "diff";
		tree: string | null; // JSON string (legacy flat tree)
		usage: string | null; // JSON string
		metadata: string | null; // JSON string
	};
	trees: {
		repoPath: string;
		id: string; // Renamed from hash for consistency
		entries: string; // JSON string of Record<string, TreeEntry>
		createdAt: number;
	};
	files: {
		id: string; // CAS hash
		path: string;
		content: string;
		encoding: string;
		size: number;
		updatedAt: number;
		author: string;
	};
	reflog: {
		id: string;
		repoPath: string;
		ref: string;
		oldHead: string | null;
		newHead: string;
		author: string;
		message: string;
		timestamp: number;
		operation: string;
	};
	stashes: {
		id: string;
		repoPath: string;
		branch: string;
		nodeId: string;
		data: string; // JSON string
		tree: string; // JSON string
		label: string;
		createdAt: number;
	};
	claims: {
		repoPath: string;
		branch: string;
		path: string; // encoded path
		author: string;
		timestamp: number;
		expiresAt: number;
	};
	telemetry: {
		id: string;
		repoPath: string;
		agentId: string;
		taskId: string | null;
		promptTokens: number;
		completionTokens: number;
		totalTokens: number;
		modelId: string;
		cost: number;
		timestamp: number;
		environment: string; // JSON string
	};
	telemetry_aggregates: {
		repoPath: string;
		id: string; // 'global', 'agent_{id}', 'task_{id}'
		totalCommits: number;
		totalTokens: number;
		totalCost: number;
	};
	agents: {
		id: string; // agentId
		userId: string;
		name: string;
		role: string;
		permissions: string; // JSON string
		memoryLayer: string; // JSON string
		createdAt: number;
		lastActive: number;
	};
	knowledge: {
		id: string; // itemId
		userId: string;
		type: string;
		content: string;
		tags: string; // JSON string
		edges: string; // JSON string
		inboundEdges: string; // JSON string
		embedding: string | null; // JSON string
		confidence: number;
		hubScore: number;
		expiresAt: number | null;
		metadata: string; // JSON string
		createdAt: number;
	};
	tasks: {
		id: string; // taskId
		userId: string;
		agentId: string;
		status: string;
		description: string;
		complexity: number;
		linkedKnowledgeIds: string; // JSON string
		result: string | null; // JSON string
		createdAt: number;
		updatedAt: number;
	};
	audit_events: {
		id: string;
		userId: string;
		agentId: string | null;
		type: string;
		data: string;
		createdAt: number;
	};
	settings: {
		id: string;
		key: string;
		value: string;
		updatedAt: number;
	};
	logical_constraints: {
		id: string;
		repoPath: string;
		pathPattern: string; // glob pattern
		knowledgeId: string;
		severity: "blocking" | "warning";
		createdAt: number;
	};
	knowledge_edges: {
		sourceId: string;
		targetId: string;
		type: string;
		weight: number;
	};
	decisions: {
		id: string;
		repoPath: string;
		agentId: string;
		taskId: string | null;
		decision: string;
		rationale: string;
		knowledgeIds: string; // JSON array of contributing knowledge
		timestamp: number;
	};
	queue_jobs: {
		id: string;
		payload: string;
		status: "pending" | "processing" | "done" | "failed";
		priority: number;
		attempts: number;
		maxAttempts: number;
		runAt: number;
		error: string | null;
		createdAt: number;
		updatedAt: number;
	};
	queue_settings: {
		id: string;
		key: string;
		value: string;
		updatedAt: number;
	};

	// --- HIVE-SPECIFIC TABLES (DietCode Mapping) ---
	hive_kb: {
		id: string;
		knowledge_key: string;
		knowledge_value: string;
		type: string;
		confidence: number;
		tags: string;
		metadata: string | null;
		created_at: string | number;
	};
	hive_healing_proposals: {
		id: string;
		violation_id: string;
		violation: string;
		rationale: string;
		proposed_code: string;
		status: string;
		confidence: number | null;
		created_at: string | number;
		applied_at: string | null;
	};
	hive_snapshots: {
		id: string;
		path: string;
		content: string;
		timestamp: number;
		hash: string;
		mtime: number | null;
	};
	hive_file_context: {
		id: string;
		path: string;
		state: string;
		source: string;
		last_read_date: number | null;
		last_edit_date: number | null;
		signature: string | null;
		external_edit_detected: number;
	};
	hive_audit: {
		id: string;
		session_id: string | null;
		type: string;
		message: string;
		data: string | null;
		timestamp: number;
	};
	hive_agent_sessions: {
		id: string;
		agent_id: string;
		status: string;
		start_time: number;
		end_time: number | null;
	};
	hive_joy_imports: {
		id: string;
		source_path: string;
		imported_path: string;
	};
	hive_joy_history: {
		id: string;
		violation_count: number;
		file_count: number;
		timestamp: number;
	};
	hive_metabolic_telemetry: {
		id: string;
		task_id: string | null;
		reads: number;
		writes: number;
		lines_added: number;
		lines_deleted: number;
		tokens_processed: number;
		verifications_success: number;
		timestamp: number;
	};
	hive_tasks: {
		id: string;
		task_id: string;
		title: string;
		objective: string;
		state: string;
		priority: number;
		vitals_heartbeat: string | null;
		v_token: string | null;
		initial_context: string | null;
		created_at: number;
		updated_at: number;
		started_at: number | null;
		completed_at: number | null;
		user_agent: string;
	};
	hive_llm_telemetry: {
		id: string;
		repo_path: string;
		agent_id: string;
		task_id: string | null;
		prompt_tokens: number | null;
		completion_tokens: number | null;
		total_tokens: number | null;
		model_id: string | null;
		cost: number | null;
		timestamp: number;
		environment: string | null;
	};
	hive_joy_metrics: {
		id: string;
		path: string;
		violation_count: number;
		hash: string;
		last_scanned: number;
	};
	hive_joy_bypasses: {
		id: string;
		path: string;
		violation_type: string;
		timestamp: number;
	};
	hive_locks: {
		id: string;
		resource: string;
		owner_id: string;
		lock_code: string;
		expires_at: number;
		acquired_at: number;
	};
	hive_queue: {
		id: string;
		type: string;
		status: string;
		total_shards: number;
		completed_shards: number | null;
		metadata: string | null;
		created_at: number;
		updated_at: number;
	};
	hive_job_results: {
		id: string;
		task_id: string;
		shard_id: number;
		status: string;
		payload: string | null;
		error: string | null;
		priority: number | null;
		timestamp: number;
	};
}
