import { setDbPath, getDb } from './infrastructure/db/Config.js';
import { dbPool } from './infrastructure/db/pool/index.js';
import * as path from 'node:path';
import * as fs from 'node:fs';

async function run() {
    // USE ACTUAL DIETCODE DB
    const dbPath = '/Users/bozoegg/Downloads/DietCode/broccoliq.db';
    if (!fs.existsSync(dbPath)) {
        console.error('DIETCODE DB NOT FOUND at:', dbPath);
        return;
    }
    setDbPath(dbPath);
    
    // Ensure initialized
    const db = await getDb('main');
    // @ts-ignore
    const tableInfo = await db.executeQuery({ sql: 'PRAGMA table_info(queue_settings)', parameters: [], query: { kind: 'RawQuery' } });
    console.log('Table queue_settings info in REAL DB:', JSON.stringify(tableInfo.rows, null, 2));

    try {
        console.log('Testing selectOne on queue_settings in REAL DB...');
        const lastMaint = await dbPool.selectOne(
            "queue_settings",
            { column: "key", value: "last_maintenance" }
        );
        console.log('Result:', lastMaint);
    } catch (e) {
        console.error('FAILED selectOne in REAL DB:', e);
    }
    
    await dbPool.stop();
}

run();
