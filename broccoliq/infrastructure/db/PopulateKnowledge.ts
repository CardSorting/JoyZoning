import { dbPool } from "./pool/index.js";
import { setDbPath } from "./Config.js";
import * as path from "node:path";
import * as fs from "node:fs";

const BENCH_DIR = path.resolve(process.cwd(), "benchmarks");
const MAIN_DB = path.join(BENCH_DIR, "bench_main.db");

// Use the benchmark DB if it exists, otherwise use the default
const dbPath = fs.existsSync(MAIN_DB) ? MAIN_DB : path.resolve(process.cwd(), "broccoliq.db");

async function main() {
	console.log(`Using database: ${dbPath}`);
	setDbPath(dbPath);

	const knowledgeItems = [
		{
			id: "bench-results-2026-03",
			userId: "system",
			type: "performance_benchmark",
			content: "Sovereign Swarm Benchmark Results (March 2026): Throughput 1.5M ops/sec (Quantum Boost), 139k ops/sec (Standard), 4-Shard Scaling 679k ops/sec (3.7x).",
			tags: JSON.stringify(["performance", "benchmark", "sharding"]),
			metadata: JSON.stringify({
				timestamp: Date.now(),
				version: "1.0.0",
				shards: 4,
				latency_p95: "0.40ms",
			}),
			createdAt: Date.now(),
		},
		{
			id: "arch-3r-rule",
			userId: "system",
			type: "architectural_constraint",
			content: "The 3R Rule for BroccoliDB: Read (Pre-load using Agent Shadows), Reduce (Batch operations 100-1000), Reorder (Parallelize across shards).",
			tags: JSON.stringify(["architecture", "best_practices"]),
			metadata: JSON.stringify({ priority: "high" }),
			createdAt: Date.now(),
		},
	];

	for (const item of knowledgeItems) {
		await dbPool.push({
			type: "insert",
			table: "knowledge",
			values: item,
		});
	}

	await dbPool.flush();
	await dbPool.stop();
	console.log("KnowledgeBase populated successfully.");
}

main().catch(console.error);
