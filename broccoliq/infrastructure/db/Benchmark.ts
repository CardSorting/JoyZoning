import * as fs from "node:fs";
import * as path from "node:path";
import { performance } from "node:perf_hooks";
import { dbPool } from "./pool/index.js";
import { setDbPath } from "./Config.js";
import { integrityWorker } from "./IntegrityWorker.js";

// Force WARN level for benchmarks to reduce console noise
process.env.LOG_LEVEL = "WARN";

const BENCH_DIR = path.resolve(process.cwd(), "benchmarks");
const MAIN_DB = path.join(BENCH_DIR, "bench_main.db");

async function setup() {
	if (fs.existsSync(BENCH_DIR)) {
		fs.rmSync(BENCH_DIR, { recursive: true, force: true });
	}
	fs.mkdirSync(BENCH_DIR, { recursive: true });
	setDbPath(MAIN_DB);
}

async function cleanup() {
	await dbPool.stop();
	integrityWorker.stop();
	if (fs.existsSync(BENCH_DIR)) {
		fs.rmSync(BENCH_DIR, { recursive: true, force: true });
	}
}

async function runThroughputBench(count: number, batchSize: number) {
	console.log(`\n--- Throughput Benchmark: ${count} operations (Batch Size: ${batchSize}) ---`);
	const start = performance.now();

	for (let i = 0; i < count; i += batchSize) {
		const ops = Array.from({ length: Math.min(batchSize, count - i) }, (_, j) => ({
			type: "insert" as const,
			table: "telemetry" as const,
			values: {
				id: `bench-${i + j}`,
				repoPath: "bench/repo",
				agentId: "agent-1",
				promptTokens: 100,
				completionTokens: 50,
				totalTokens: 150,
				modelId: "gpt-4",
				cost: 0.01,
				timestamp: Date.now(),
				environment: "{}",
			},
		}));
		for (const op of ops) {
			await dbPool.push(op);
		}
	}

	await dbPool.flush();
	const duration = performance.now() - start;
	const throughput = (count / (duration / 1000)).toFixed(2);
	console.log(`Finished in ${duration.toFixed(2)}ms (${throughput} ops/sec)`);
	return Number(throughput);
}

async function runShardingBench(count: number, shardCount: number) {
	console.log(`\n--- Sharding Benchmark: ${count} operations across ${shardCount} shards ---`);
	const start = performance.now();

	const shardIds = Array.from({ length: shardCount }, (_, i) => `shard-${i}`);

	for (let i = 0; i < count; i++) {
		const shardId = shardIds[i % shardCount];
		await dbPool.push({
			type: "insert",
			table: "telemetry",
			values: {
				id: `shard-bench-${i}`,
				repoPath: `bench/repo/${shardId}`,
				agentId: "agent-1",
				promptTokens: 10,
				completionTokens: 5,
				totalTokens: 15,
				modelId: "gpt-4",
				cost: 0.001,
				timestamp: Date.now(),
				environment: "{}",
			},
			shardId,
		});
	}

	await dbPool.flush();
	const duration = performance.now() - start;
	const throughput = (count / (duration / 1000)).toFixed(2);
	console.log(`Finished in ${duration.toFixed(2)}ms (${throughput} ops/sec)`);
	return Number(throughput);
}

async function runLockingBench(count: number) {
	console.log(`\n--- Locking Benchmark: ${count} lock/release cycles ---`);
	const start = performance.now();

	for (let i = 0; i < count; i++) {
		const resource = `resource-${i % 10}`;
		const author = `agent-${i % 5}`;
		const acquired = await dbPool.acquireLock(resource, author, "main", 1000);
		if (acquired) {
			await dbPool.releaseLock(resource, author, "main");
		}
	}

	const duration = performance.now() - start;
	const latency = (duration / count).toFixed(2);
	console.log(`Finished in ${duration.toFixed(2)}ms (${latency}ms avg latency)`);
}

async function runIntegrityWorkerBench(count: number) {
	console.log("\n--- Integrity Worker Impact Test ---");
	console.log("Starting background integrity audits...");
	integrityWorker.start(); // Runs every 10 mins, plus initial run in 5s

    // Manually trigger audit to ensure overlap
    const auditPromise = integrityWorker.runAudit();

	const throughput = await runThroughputBench(count, 100);
    await auditPromise;
    
	console.log(`Throughput during audit: ${throughput} ops/sec`);
}

async function main() {
	try {
		await setup();

		// 1. Throughput
		await runThroughputBench(10000, 100);
		await runThroughputBench(10000, 1000);

		// 2. Sharding
		await runShardingBench(10000, 1);
		await runShardingBench(10000, 4);

		// 3. Locking
		await runLockingBench(100);

		// 4. Integrity Worker Impact
		await runIntegrityWorkerBench(10000);

		console.log("\nBenchmarks complete.");
	} catch (e) {
		console.error("Benchmark failed", e);
	} finally {
		await cleanup();
	}
}

main().catch(console.error);
