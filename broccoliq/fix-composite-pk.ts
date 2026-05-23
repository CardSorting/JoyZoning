/**
 * [BAND-AID SYSTEM HARDENING]
 * Purpose: Add standalone 'id' column to composite PK tables (branches, tags)
 */

import Database from 'better-sqlite3';
import * as crypto from 'crypto';

const DB_PATH = '/Users/bozoegg/Downloads/broccolidb/broccoliq.db';
const TABLES = ['branches', 'tags'];

const db = new Database(DB_PATH);

try {
  console.log('=== BROCCOLIDB BAND-AID SEQUENCE ===');
  
  for (const tableName of TABLES) {
    console.log(`\n[BAND-AID] Processing table '${tableName}'`);
    
    const result = db.pragma(`table_info('${tableName}')`) as any;
    const tableInfo = Array.isArray(result) ? result : [];
    
    const hasId = tableInfo.some((row: any) => row[1] === 'id');
    if (hasId) {
      console.log(`[BAND-AID] ✓ 'id' exists, skipping`);
      continue;
    }
    
    console.log(`[BAND-AID] Adding 'id' PRIMARY KEY to ${tableName}`);
    console.log(`[BAND-AID] Schema columns:`, tableInfo.map((r: any) => `${r[1]} (${r[2]})`).join(', '));
    db.exec('BEGIN TRANSACTION');
    
    // Build column definitions - use indices [1] for name, [2] for type
    const colDef: string[] = [`id TEXT PRIMARY KEY`];
    
    tableInfo.forEach((row: any) => {
      const colName = row[1];
      if (colName !== 'id') {
        const colType = row[2] || 'TEXT';
        colDef.push(`${colName} ${colType}`);
      }
    });
    
    const colDefinitions = colDef.join(', ');
    
    // Create new table
    db.exec(`CREATE TABLE ${tableName}_new (${colDefinitions})`);
    
    // Insert data - use explicit column indices
    let insertCols = '';
    let selectCols = '';
    let colNames: string[] = [];
    tableInfo.forEach((row: any) => {
      const colName = row[1];
      if (colName !== 'id') {
        colNames.push(colName);
        if (insertCols) {
          insertCols += ', ' + colName;
          selectCols += ', ' + colName;
        } else {
          insertCols = colName;
          selectCols = colName;
        }
      }
    });
    
    console.log(`[BAND-AID] Selecting columns:`, selectCols);
    
    const insertSql = `INSERT INTO ${tableName}_new (id, ${insertCols}) SELECT '${crypto.randomUUID()}', ${selectCols} FROM ${tableName}`;
    console.log(`[BAND-AID] Insert SQL: ${insertSql}`);
    db.exec(insertSql);
    
    // Drop old, rename new
    db.exec(`DROP TABLE ${tableName}`);
    db.exec(`ALTER TABLE ${tableName}_new RENAME TO ${tableName}`);
    db.exec('COMMIT');
    
    console.log(`[BAND-AID] ✅ '${tableName}' fixed`);
  }
  
  console.log('\n✅ BAND-AID COMPLETE');
} catch (error: any) {
  console.error('❌ FAILED:', error.message);
  process.exit(1);
} finally {
  db.close();
}