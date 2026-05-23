import { getRawDb } from './infrastructure/db/Config.js';

async function run() {
    const rawDb = await getRawDb('main') as any;
    try {
        console.log('Testing SQL with double-double quoted column in ON CONFLICT...');
        // SQLite will parse ""key"" as a column named "key" (with literal quotes)
        rawDb.prepare('INSERT INTO queue_settings (id, key, value, updatedAt) VALUES (?, ?, ?, ?) ON CONFLICT(""key"") DO UPDATE SET value = ?').run('1', 'key1', 'val1', Date.now(), 'val1');
    } catch (e) {
        console.error('ERROR with double-double quoted column:', e);
    }
}

run();
