
import { Connection } from '../core/connection.js';
import { AgentContext } from '../core/agent-context.js';
import { Workspace } from '../core/workspace.js';
import * as path from 'path';

async function run() {
  const dbPath = path.resolve(process.cwd(), '../broccolidb.db');
  const conn = new Connection({ dbPath });
  const pool = conn.getPool();
  await pool.ensureDb();
  
  const userId = 'local-user';
  const workspaceId = 'local-workspace';
  const workspace = new Workspace(pool, userId, workspaceId);
  await workspace.init();
  const context = new AgentContext(workspace, pool, userId);
  
  const tagsStr = 'database,hardening';
  const tagsArray = tagsStr ? tagsStr.split(',').map(t => t.trim()).filter(Boolean) : [];
  const newId = await context.addKnowledge('test-node', 'fact' as any, 'Hardened BufferedDbPool transaction sorting to handle reverse topological deletes and stable push sequencing', { tags: tagsArray });
  console.log(JSON.stringify({ success: true, kbId: newId }));
}
run().catch(err => {
  console.error(err);
  process.exit(1);
});
