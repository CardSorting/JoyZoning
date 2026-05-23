import { getRawDb } from './infrastructure/db/Config.js';
import * as path from 'node:path';

async function run() {
    const rawDb = await getRawDb('main') as any;
    try {
        console.log('Testing SQL with double quoted value...');
        rawDb.prepare('SELECT * FROM queue_settings WHERE key = "last_maintenance"').all();
    } catch (e) {
        console.error('ERROR with double quoted value:', e);
    }

    try {
        console.log('Testing SQL with double quoted column...');
        rawDb.prepare('SELECT * FROM queue_settings WHERE "key" = ?').all('last_maintenance');
        console.log('SUCCESS with double quoted column');
    } catch (e) {
        console.error('ERROR with double quoted column:', e);
    }
}

run();
