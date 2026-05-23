import { setDbPath, getDb, getRawDb } from './infrastructure/db/Config.js';
import { dbPool } from './infrastructure/db/pool/index.js';
import * as path from 'node:path';
import * as fs from 'node:fs';

async function run() {
    const dbPath = path.resolve(process.cwd(), 'migration_test.db');
    if (fs.existsSync(dbPath)) fs.unlinkSync(dbPath);
    
    // Create LEGACY database with 'name' instead of 'key'
    // @ts-ignore
    const Database = (await import("better-sqlite3")).default;
    const legacyDb = new Database(dbPath);
    legacyDb.exec(`CREATE TABLE queue_settings (
        id TEXT PRIMARY KEY,
        name TEXT NOT NULL UNIQUE,
        value TEXT,
        updatedAt BIGINT
    )`);
    legacyDb.prepare("INSERT INTO queue_settings (id, name, value, updatedAt) VALUES (?, ?, ?, ?)").run('1', 'last_maintenance', '12345', Date.now());
    legacyDb.close();

    console.log('--- STARTING APP WITH LEGACY DB ---');
    setDbPath(dbPath);
    
    // This should trigger migration
    await getDb('main');
    
    try {
        console.log('Testing selectOne on migrated queue_settings...');
        const lastMaint = await dbPool.selectOne(
            "queue_settings",
            { column: "key", value: "last_maintenance" }
        );
        console.log('Result:', lastMaint);
        if (lastMaint && (lastMaint as any).key === 'last_maintenance') {
            console.log('✅ MIGRATION SUCCESSFUL!');
        } else {
            console.error('❌ MIGRATION FAILED - result:', lastMaint);
        }
    } catch (e) {
        console.error('FAILED after migration:', e);
    }
    
    await dbPool.stop();
    if (fs.existsSync(dbPath)) fs.unlinkSync(dbPath);
}

run();
