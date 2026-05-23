import { SqliteQueue } from "./infrastructure/queue/SqliteQueue.js";
import { dbPool } from "./infrastructure/db/pool/index.js";

async function runLeakTest() {
    console.log("Starting Leak Detection Test for SqliteQueue...");
    
    const queue = new SqliteQueue<string>({ shardId: "leak_test" });
    
    // We will run the processing loop with a very short poll interval
    // and wait for a few cycles.
    // The MaxListenersExceededWarning typically triggers after 10 listeners.
    // We'll wait 20 times.
    
    let cycles = 0;
    const maxCycles = 20;
    
    console.log(`Running ${maxCycles} poll cycles with no jobs...`);
    
    // Use a handler that does nothing
    queue.process(async (job) => {
        // This won't be called if no jobs exist
    }, { pollIntervalMs: 10 });
    
    // Wait for the cycles to pass
    // Each cycle without a job will attempt to add a wait listener
    while (cycles < maxCycles) {
        await new Promise(resolve => setTimeout(resolve, 50));
        cycles++;
        if (cycles % 5 === 0) console.log(`  Cycle ${cycles}/${maxCycles}...`);
    }
    
    console.log("✅ Finished cycles. Check output for MaxListenersExceededWarning.");
    
    queue.stop();
    await dbPool.stop();
    process.exit(0);
}

runLeakTest().catch(e => {
    console.error(e);
    process.exit(1);
});
