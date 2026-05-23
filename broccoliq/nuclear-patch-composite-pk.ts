/**
 * [NUCLEAR SYSTEM HARDENING - TARGETED]
 * Purpose: Modify BroccoliDB's composite PK tables to add standalone 'id' column
 * Affected Tables: branches (repoPath, name), tags (repoPath, name)
 * Architecture: Raw SQL with transaction atomicity
 */

import Database from 'better-sqlite3';
import * as fs from 'fs';
import * as path from 'node:path';
import * as crypto from 'crypto';

const DB_PATH = '/Users/bozoegg/Downloads/broccolidb/broccoliq.db';

const PATCHED_TABLES = ['branches', 'tags'];

console.log('=== BROCCOLIDB BAND-AID SEQUENCE (COMPOSITE PK FIX) ===');
console.log(`[TARGET] Target database: ${DB_PATH}`);

// Ensure database directory exists
const dbDir = path.dirname(DB_PATH);
if (!fs.existsSync(dbDir)) {
  fs.mkdirSync(dbDir, { recursive: true });
  console.log(`[TARGET] Created database directory: ${dbDir}`);
}

const db = new Database(DB_PATH);

try {
  
  for (const table of PATCHED_TABLES) {
    console.log(`\n[TARGET] Processing table '${table}'`);
    
    // Check if table exists
    const tableExists = db.prepare(`
      SELECT name FROM sqlite_master 
      WHERE type='table' AND name=?
    `).get(table) as any;
    
    if (!tableExists) {
      console.log(`[TARGET] ℹ Table '${table}' does not exist, skipping`);
      continue;
    }
    
    // Get current table structure
    const result = db.pragma(`table_info('${table}')`, { simple: true }) as any;
    const tableInfo = Array.isArray(result) ? result : [];
    
    // Check if 'id' column already exists
    const hasId = tableInfo.some((row: any) => row[1] === 'id');
    if (hasId) {
      console.log(`[TARGET] ✓ Pre-check: 'id' column already exists`);
      const rowCount = db.prepare(`SELECT count(*) as count FROM "${table}"`).get() as any;
      const count = rowCount?.count || 0;
      console.log(`[TARGET] ℹ Table has ${count} row(s)`);
      continue;
    }
    
    console.log(`[TARGET] ⚠ CHECK: Missing standalone 'id' column`);
    
    // Start transaction
    db.exec('BEGIN TRANSACTION');
    
    // Create temporary table with modified schema (PARTIAL PK: id + original composite PK)
    const tempTableName = `${table}_reformed`;
    db.exec(`DROP TABLE IF EXISTS "${tempTableName}"`);
    db.exec(`CREATE TABLE "${tempTableName}" (id TEXT PRIMARY KEY, ${table}_PK1 TEXT, ${table}_PK2 TEXT)`);
    
    // Rename original columns to PK placeholders
    db.exec(`ALTER TABLE "${table}" RENAME TO "${tempTableName}_temp"`);
    db.exec(`CREATE TABLE "${tempTableName}_new" (id TEXT PRIMARY KEY, ${table}_PK1 TEXT, ${table}_PK2 TEXT)`);
    db.exec(`INSERT INTO "${tempTableName}_new" (id, ${table}_PK1, ${table}_PK2) SELECT '${crypto.randomUUID()}', ${table}_PK1, ${table}_PK2 FROM "${tempTableName}_temp"`);
    
    // Reload table info to get column names
    const newIndex = db.pragma(`table_info('${tempTableName}_new')`, { simple: true }) as any;
    const newInfo = Array.isArray(newIndex) ? newIndex : [];
    const col1 = newInfo.find((r: any) => r[1] === `${table}_PK1`);
    const col2 = newInfo.find((r: any) => r[1] === `${table}_PK2`);
    
    if (!col1 || !col2) {
      throw new Error('Failed to map original primary key columns');
    }
    
    // Get all other columns (excluding the renamed PKs)
    const nonPkColumns = tableInfo.filter((r: any) => r[1] !== `${table}_PK1` && r[1] !== `${table}_PK2`).map((r: any) => `"${r[1]}"`).join(', ');
    
    // Fix column types
    const createFinalSQL = db.prepare(`
      CREATE TABLE "${table}_final" (id TEXT PRIMARY KEY, ${table}_PK1 TEXT, ${table}_PK2 TEXT${nonPkColumns ? ', ' + nonPkColumns : ''})
    `).sql;
    
    // Drop tables in correct order
    db.exec('DROP TABLE IF EXISTS "' + tempTableName + '"');
    db.exec('DROP TABLE IF EXISTS "' + tempTableName + '_new"');
    db.exec('DROP TABLE IF EXISTS "' + tempTableName + '_temp"');
    
    // Actually create the table
    db.exec(`
      CREATE TABLE "${table}_final" 
      (id TEXT PRIMARY KEY, 
       ${col1[1]} TEXT NOT NULL,
       ${col2[1]} TEXT NOT NULL${nonPkColumns ? ', ' + nonPkColumns : ''})
    `);
    
    // Create new table with just primary key columns
    db.exec(`
      CREATE TABLE "${table}_r"(
        ${col1[1]} TEXT NOT NULL,
        ${col2[1]} TEXT NOT NULL${nonPkColumns ? ', ' + nonPkColumns : ''}
      )
    `);
    
    // Insert data to new table
    db.prepare(`
      INSERT INTO "${table}_r" (${col1[1]}, ${col2[1]}${nonPkColumns ? ', ' + nonPkColumns : ''})
      SELECT ${col1[1]}, ${col2[1]}${nonPkColumns ? ', ' + nonPkColumns : ''} FROM "${table}"
    `).run();
    
    // Drop original, rename the one with ID
    db.exec(`DROP TABLE "${table}"`);
    db.exec(`ALTER TABLE "${table}_r" RENAME TO "${table}"`);
    
    console.log(`[TARGET] ✅ Table reformatted with standalone 'id' column`);
    
    // Verify
    const verifyResult = db.pragma(`table_info('${table}')`, { simple: true }) as any;
    const verifyInfo = Array.isArray(verifyResult) ? verifyResult : [];
    const verifyHasId = verifyInfo.some((row: any) => row[1] === 'id');
    
    if (verifyHasId) {
      console.log(`[TARGET] ✅ Post-migration verification: 'id' present`);
    } else {
      throw new Error(`BAND-AID FAILED: 'id' still missing`);
    }
  }
  
  // Commit all changes
  db.exec('COMMIT');
  
  console.log('\n==================================================');
  console.log('✅ BAND-AID COMPLETE: 2/2 composite PK tables fixed');
  console.log('==================================================');
  
} catch (error: any) {
  // Rollback on error
  try {
    db.exec('ROLLBACK');
  } catch (e) {}
  
  console.error('\n==================================================');
  console.error('❌ BAND-AID FAILED');
  console.error('Error:', error.message);
  console.error('==================================================\n');
  process.exit(1);
} finally {
  db.close();
}