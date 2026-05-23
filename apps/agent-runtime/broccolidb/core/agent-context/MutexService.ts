import type { ServiceContext } from './types.js';
import { getDb } from '../../infrastructure/db/Config.js';

/**
 * MutexService provides fault-tolerant distributed locking.
 * Absorbed from src/utils/cronTasksLock.ts.
 * Uses a 'Sovereign Fencing Token' to prevent split-brain graph corruption.
 */
export class MutexService {
  private _fencingToken: number = Date.now();
  private _heartbeats: Map<string, NodeJS.Timeout> = new Map();

  constructor(private ctx: ServiceContext) {}

  /**
   * Acquires a lock on a shared resource with active heartbeats.
   * If the lock is held by a dead PID, it automatically annexes it.
   * Returns a fencing token if successful, or null otherwise.
   */
  async acquireLock(resource: string): Promise<number | null> {
    console.log(`[Mutex] 🛡️ Attempting to acquire lock: ${resource}...`);
    
    const db = await getDb();
    
    // Execute lock acquisition atomically inside a transaction to guarantee serialized isolation
    return await db.transaction().execute(async (trx) => {
      const existing = await trx
        .selectFrom('swarm_locks')
        .selectAll()
        .where('resource', '=', resource)
        .executeTakeFirst();
      
      if (existing) {
        const pid = Number(existing.ownerId);
        const expiresAt = Number(existing.expiresAt);
        const isExpired = Date.now() > expiresAt;
        const isDead = !this._isProcessAlive(pid);
        
        if (isDead || isExpired) {
          console.log(`[Mutex] 💀 Detected ${isDead ? 'stale' : 'expired'} lock from Owner ${pid}. Annexing...`);
          await trx
            .deleteFrom('swarm_locks')
            .where('resource', '=', resource)
            .execute();
        } else {
          console.warn(`[Mutex] 🔒 Resource ${resource} is locked by active PID ${pid}.`);
          return null;
        }
      }

      // Increment fencing token
      this._fencingToken++;
      const token = this._fencingToken;
      
      try {
        await trx
          .insertInto('swarm_locks')
          .values({
            resource,
            ownerId: process.pid.toString(),
            expiresAt: Date.now() + 60000, // 60s initial TTL
            createdAt: Date.now()
          })
          .execute();
      } catch (err) {
        console.error(`[Mutex] ❌ Failed to insert lock for ${resource}. Likely a race condition.`, err);
        return null;
      }

      this._startHeartbeat(resource);
      return token;
    });
  }

  /**
   * Releases a lock and stops heartbeats.
   */
  async releaseLock(resource: string): Promise<void> {
    console.log(`[Mutex] 🔓 Releasing lock: ${resource}`);
    const hb = this._heartbeats.get(resource);
    if (hb) {
      clearInterval(hb);
      this._heartbeats.delete(resource);
    }

    const db = await getDb();
    await db
      .deleteFrom('swarm_locks')
      .where('resource', '=', resource)
      .execute();
  }

  private _startHeartbeat(resource: string) {
    if (this._heartbeats.has(resource)) return;

    const interval = setInterval(async () => {
      try {
        const db = await getDb();
        // Verify we still own the lock before heartbeat
        const existing = await db
          .selectFrom('swarm_locks')
          .selectAll()
          .where('resource', '=', resource)
          .executeTakeFirst();
        
        if (!existing || existing.ownerId !== process.pid.toString()) {
          console.error(`[Mutex] ⚠️ Lost lock ownership for ${resource}. Stopping heartbeat.`);
          this._stopHeartbeat(resource);
          return;
        }

        console.log(`[Mutex] 💓 Heartbeat for ${resource}...`);
        await db
          .updateTable('swarm_locks')
          .set({ expiresAt: Date.now() + 60000 })
          .where('resource', '=', resource)
          .execute();
      } catch (err) {
        console.error(`[Mutex] ❌ Heartbeat failed for ${resource}`, err);
      }
    }, 20000); // Pulse every 20s

    this._heartbeats.set(resource, interval);
  }

  private _stopHeartbeat(resource: string) {
    const hb = this._heartbeats.get(resource);
    if (hb) {
      clearInterval(hb);
      this._heartbeats.delete(resource);
    }
  }

  private _isProcessAlive(pid: number): boolean {
    try {
      if (pid === process.pid) return true;
      // Signal 0 probes process existence without sending signals
      process.kill(pid, 0);
      return true;
    } catch (err: any) {
      return err.code === 'EPERM'; // Alive but permission denied
    }
  }

  public get fencingToken(): number {
    return this._fencingToken;
  }

  public shutdown() {
    for (const hb of this._heartbeats.values()) {
      clearInterval(hb);
    }
    this._heartbeats.clear();
  }
}
