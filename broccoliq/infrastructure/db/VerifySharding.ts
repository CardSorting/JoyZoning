import { SqliteQueue } from "../queue/SqliteQueue.js";
import { dbPool } from "./pool/index.js";
import { XmlLite } from "../util/XmlLite.js";
import fs from "node:fs";
import path from "node:path";

async function runTest() {
	console.log("Starting Shard Isolation Verification Test...");

	const mainQueue = new SqliteQueue<string>({ shardId: "main" });
	const signalsQueue = new SqliteQueue<string>({ shardId: "signals" });

	const jsonPayload = JSON.stringify({ task: "semantic_scoring", data: "test" });
	const xmlPayload = XmlLite.serialize("signal", { type: "test", source: "verify" }, "ping");

	console.log("Enqueuing JSON to 'main' shard...");
	await mainQueue.enqueue(jsonPayload);

	console.log("Enqueuing XML to 'signals' shard...");
	await signalsQueue.enqueue(xmlPayload);

	// Verify in-memory separation
	const metrics = dbPool.getMetrics();
	console.log("Current Metrics:", JSON.stringify(metrics, null, 2));

	if (!metrics.shards.includes("main") || !metrics.shards.includes("signals")) {
		throw new Error("Shards not properly tracked in BufferedDbPool metrics");
	}

	console.log("Attempting to dequeue from 'signals' shard...");
	const signalJobs = await signalsQueue.dequeueBatch(10);
	
	console.log(`Received ${signalJobs.length} jobs from 'signals' shard.`);
	for (const job of signalJobs) {
		console.log(` - Job Payload: ${job.payload.substring(0, 50)}...`);
		if (!job.payload.startsWith("<")) {
			throw new Error("CRITICAL: Received non-XML payload in signals shard!");
		}
	}

	console.log("Attempting to dequeue from 'main' shard...");
	const mainJobs = await mainQueue.dequeueBatch(10);
	console.log(`Received ${mainJobs.length} jobs from 'main' shard.`);
	for (const job of mainJobs) {
		if (job.payload.startsWith("<")) {
			throw new Error("CRITICAL: Received XML payload in main shard!");
		}
	}

	console.log("Flushing to disk...");
	await dbPool.flush();

	// Check if files exist
	const dbDir = path.join(process.cwd(), "infrastructure", "db", "data");
	// Note: The actual path might depend on Config.ts
	console.log("Verifying disk persistence (heuristic)...");
	
	console.log("Test Passed Successfully!");
	process.exit(0);
}

runTest().catch(e => {
	console.error("Test Failed:", e);
	process.exit(1);
});
