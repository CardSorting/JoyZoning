import { getRawDb } from './infrastructure/db/Config.js';

async function run() {
    const rawDb = await getRawDb('main') as any;
    try {
        console.log('Testing SQL with "key" in double quotes...');
        rawDb.prepare('INSERT INTO queue_settings (id, key, value, updatedAt) VALUES (?, ?, ?, ?) ON CONFLICT("key") DO UPDATE SET value = ?').run('1', 'key1', 'val1', Date.now(), 'val1');
        console.log('SUCCESS with "key"');
    } catch (e) {
        console.error('ERROR with "key":', e);
    }

    try {
        console.log('Testing SQL with ""key"" (escaped quotes)...');
        rawDb.prepare('INSERT INTO queue_settings (id, key, value, updatedAt) VALUES (?, ?, ?, ?) ON CONFLICT(""key"") DO UPDATE SET value = ?').run('2', 'key2', 'val2', Date.now(), 'val2');
    } catch (e) {
        console.error('ERROR with ""key"":', e);
    }
}

run();
