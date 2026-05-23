import { dbPool } from "./infrastructure/db/pool/index.js";
import { getDb } from "./infrastructure/db/Config.js";
import { CompiledQuery } from "kysely";

async function repro() {
    const db = await getDb();
    
    // Check schema
    const info = await db.executeQuery(CompiledQuery.raw("PRAGMA table_info(queue_settings)"));
    console.log("Schema of queue_settings:", JSON.stringify(info.rows, null, 2));

    try {
        console.log("Attempting first UPSERT (no ID)...");
        await dbPool.push({
            type: "upsert",
            table: "queue_settings",
            values: {
                key: "repro_test",
                value: "1",
                updatedAt: Date.now()
            },
            where: { column: "key", value: "repro_test" },
            conflictTarget: "key"
        });
        
        await dbPool.flush();
        console.log("First UPSERT success.");

        console.log("Attempting second UPSERT (no ID, should update)...");
        await dbPool.push({
            type: "upsert",
            table: "queue_settings",
            values: {
                key: "repro_test",
                value: "2",
                updatedAt: Date.now()
            },
            where: { column: "key", value: "repro_test" },
            conflictTarget: "key"
        });

        await dbPool.flush();
        console.log("Second UPSERT success.");
    } catch (e) {
        console.error("Repro failed:", e);
    } finally {
        await dbPool.stop();
    }
}

repro();
