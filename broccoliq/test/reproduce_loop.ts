/**
 * Reproduction: SqliteQueue Infinite Loop
 * Verifies that a job is only processed once and correctly marked as done.
 */

import { SqliteQueue } from "../infrastructure/queue/SqliteQueue.js";
import { dbPool } from "../infrastructure/db/BufferedDbPool.js";

async function reproduce() {
    console.log("🧪 Starting Reproduction Test: SqliteQueue Loop...");
    
    const queue = new SqliteQueue<string>({ shardId: "repro" });
    let processingCount = 0;

    // 1. Enqueue 1 job
    const jobId = await queue.enqueue("Test Loop Job");
    console.log(`✅ Job Enqueued: ${jobId}`);

    // 2. Start processing
    // We expect this to run once and then idle
    queue.process(async (job) => {
        processingCount++;
        console.log(`🩹 Job Processed [${job.id}] (Count: ${processingCount})`);
    }, { concurrency: 1, pollIntervalMs: 10 });

    // 3. Wait and observe
    await new Promise(resolve => setTimeout(resolve, 2000));

    console.log("\n--- Results ---");
    console.log(`Total Processed: ${processingCount}`);
    
    if (processingCount > 1) {
        console.error("❌ FAILURE: Job was processed multiple times! (LOOP DETECTED)");
        process.exit(1);
    } else if (processingCount === 1) {
        console.log("✅ SUCCESS: Job processed exactly once.");
        process.exit(0);
    } else {
        console.error("❌ FAILURE: Job was never processed.");
        process.exit(1);
    }
}

reproduce().catch(err => {
    console.error("💥 Test Failed:", err);
    process.exit(1);
});
