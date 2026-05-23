import { setDbPath, getDb } from './infrastructure/db/Config.js';
import { dbPool } from './infrastructure/db/pool/index.js';
import * as path from 'node:path';
import * as fs from 'node:fs';

async function run() {
    const dbPath = path.resolve(process.cwd(), 'repro_test.db');
    if (fs.existsSync(dbPath)) fs.unlinkSync(dbPath);
    setDbPath(dbPath);
    
    // Ensure initialized
    const db = await getDb('main');
    // @ts-ignore
    const tableInfo = await db.executeQuery({ sql: 'PRAGMA table_info(queue_settings)', parameters: [], query: { kind: 'RawQuery' } });
    console.log('Table queue_settings info:', JSON.stringify(tableInfo.rows, null, 2));

    try {
        console.log('Testing selectOne on queue_settings...');
        const lastMaint = await dbPool.selectOne(
            "queue_settings",
            { column: "key", value: "last_maintenance" }
        );
        console.log('Result:', lastMaint);
    } catch (e) {
        console.error('FAILED selectOne:', e);
    }
    
    await dbPool.stop();
    if (fs.existsSync(dbPath)) fs.unlinkSync(dbPath);
}

run();
