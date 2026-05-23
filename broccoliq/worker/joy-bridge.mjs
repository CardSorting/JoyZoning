#!/usr/bin/env node
/**
 * JoyZoning ↔ BroccoliQ bridge (production-hardened).
 * - localhost-only
 * - body size cap
 * - idempotent joy_event audit rows
 * - batch ingest
 */
import { createServer } from "node:http";
import { randomUUID } from "node:crypto";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const distRoot = resolve(__dirname, "../dist/infrastructure/index.js");

let setDbPath;
let dbPool;

try {
  ({ setDbPath, dbPool } = await import(distRoot));
} catch (err) {
  console.error(
    "[joy-bridge] BroccoliQ dist missing. Run: cd broccoliq && npm install && npm run build",
  );
  console.error(err);
  process.exit(1);
}

const dbPath =
  process.env.BROCCOLIQ_DB_PATH ??
  process.env.JOYZONING_BROCCOLIQ_DB_PATH ??
  resolve(process.cwd(), "broccoliq.db");

const listenHost = process.env.BROCCOLIQ_BRIDGE_HOST ?? "127.0.0.1";
const listenPort = Number.parseInt(
  process.env.BROCCOLIQ_BRIDGE_PORT ?? "9471",
  10,
);
const maxBodyBytes = Number.parseInt(
  process.env.BROCCOLIQ_MAX_BODY_BYTES ?? "1048576",
  10,
);

if (!Number.isFinite(listenPort) || listenPort < 1 || listenPort > 65535) {
  console.error("[joy-bridge] invalid BROCCOLIQ_BRIDGE_PORT");
  process.exit(1);
}

setDbPath(dbPath);

const startedAt = Date.now();
let mirrorCount = 0;
let taskSyncCount = 0;
let lastError = null;

function isLocalRequest(req) {
  const host = (req.headers.host ?? "").split(":")[0].toLowerCase();
  return host === "127.0.0.1" || host === "localhost" || host === "";
}

function json(res, status, body) {
  const payload = JSON.stringify(body);
  res.writeHead(status, {
    "Content-Type": "application/json",
    "Content-Length": Buffer.byteLength(payload),
  });
  res.end(payload);
}

async function readBody(req) {
  const chunks = [];
  let total = 0;
  for await (const chunk of req) {
    total += chunk.length;
    if (total > maxBodyBytes) {
      const err = new Error(`payload exceeds ${maxBodyBytes} bytes`);
      err.code = "PAYLOAD_TOO_LARGE";
      throw err;
    }
    chunks.push(chunk);
  }
  const raw = Buffer.concat(chunks).toString("utf8");
  if (!raw.trim()) return null;
  return JSON.parse(raw);
}

function auditIdForJoyEvent(joyEventId) {
  return `joy:${String(joyEventId)}`;
}

async function mirrorJoyEvent(body) {
  const joyEventId = body.id != null ? String(body.id) : randomUUID();
  const correlationId = body.correlationId ?? body.correlation_id ?? "";
  const source = body.source ?? "Unknown";
  const type = body.type ?? "event";
  const payloadJson = body.payloadJson ?? body.payload_json ?? "{}";
  const occurredAt = body.occurredAt ?? body.occurred_at;
  const ts =
    occurredAt != null
      ? Date.parse(String(occurredAt)) || Date.now()
      : Date.now();

  await dbPool.push(
    {
      type: "upsert",
      table: "hive_audit",
      conflictTarget: "id",
      values: {
        id: auditIdForJoyEvent(joyEventId),
        user_id: "joyzoning",
        agent_id: "control-plane",
        type: `joy.${type}`,
        message: String(source),
        data: JSON.stringify({
          joyEventId,
          correlationId,
          payloadJson,
        }),
        timestamp: ts,
      },
      shardId: "joy",
    },
    "joy-bridge",
  );
  mirrorCount += 1;
  return { ok: true, mirrored: true, joyEventId };
}

async function mirrorJoyEventsBatch(events) {
  let count = 0;
  for (const item of events) {
    await mirrorJoyEvent(item);
    count += 1;
  }
  return { ok: true, mirrored: count };
}

async function syncWorkTask(body) {
  const taskId = body.taskId ?? body.task_id;
  if (!taskId) throw new Error("taskId required");

  const now = Date.now();
  const status = body.status ?? "pending";
  const stableId = `task:${String(taskId)}`;
  const row = {
    id: stableId,
    task_id: String(taskId),
    user_id: body.userId ?? body.user_id ?? null,
    agent_id: body.agentId ?? body.agent_id ?? "joyzoning",
    title: body.title ?? "(untitled)",
    objective: body.objective ?? body.description ?? "",
    description: body.description ?? null,
    status,
    priority: Number(body.priority ?? 0),
    vitals_heartbeat: body.vitalsHeartbeat ?? null,
    v_token: body.vToken ?? null,
    initial_context: body.initialContext ?? null,
    result: body.result ?? null,
    created_at: Number(body.createdAt ?? now),
    updated_at: Number(body.updatedAt ?? now),
    started_at: body.startedAt ?? null,
    completed_at: body.completedAt ?? null,
    user_agent: body.userAgent ?? "joyzoning-control-plane/1",
  };

  await dbPool.push(
    {
      type: "upsert",
      table: "hive_tasks",
      conflictTarget: "task_id",
      values: row,
      shardId: "joy",
    },
    "joy-bridge",
  );
  taskSyncCount += 1;
  return { ok: true, taskId: String(taskId) };
}

const server = createServer(async (req, res) => {
  if (!isLocalRequest(req)) {
    return json(res, 403, { error: "forbidden", message: "localhost only" });
  }

  try {
    const url = new URL(req.url ?? "/", `http://${listenHost}:${listenPort}`);
    const method = req.method ?? "GET";

    if (method === "GET" && (url.pathname === "/health" || url.pathname === "/ready")) {
      if (url.pathname === "/ready") {
        try {
          await dbPool.flush();
        } catch (flushErr) {
          lastError = flushErr instanceof Error ? flushErr.message : String(flushErr);
          return json(res, 503, {
            status: "not_ready",
            error: lastError,
          });
        }
      }

      return json(res, 200, {
        status: "ok",
        service: "broccoliq-joy-bridge",
        dbPath,
        uptimeMs: Date.now() - startedAt,
        mirrorCount,
        taskSyncCount,
        lastError,
      });
    }

    if (method === "POST" && url.pathname === "/v1/joy-events") {
      const body = await readBody(req);
      const result = await mirrorJoyEvent(body ?? {});
      return json(res, 202, result);
    }

    if (method === "POST" && url.pathname === "/v1/joy-events/batch") {
      const body = await readBody(req);
      const events = Array.isArray(body?.events) ? body.events : [];
      if (events.length === 0) {
        return json(res, 400, { error: "bad_request", message: "events[] required" });
      }
      if (events.length > 500) {
        return json(res, 413, { error: "batch_too_large", max: 500 });
      }
      const result = await mirrorJoyEventsBatch(events);
      return json(res, 202, result);
    }

    if (method === "POST" && url.pathname === "/v1/work-tasks") {
      const body = await readBody(req);
      const result = await syncWorkTask(body ?? {});
      return json(res, 202, result);
    }

    if (method === "POST" && url.pathname === "/v1/flush") {
      await dbPool.flush();
      return json(res, 200, { ok: true, flushed: true });
    }

    json(res, 404, { error: "not_found", path: url.pathname });
  } catch (err) {
    lastError = err instanceof Error ? err.message : String(err);
    if (err?.code === "PAYLOAD_TOO_LARGE") {
      return json(res, 413, { error: "payload_too_large", message: lastError });
    }
    console.error("[joy-bridge]", err);
    json(res, 500, {
      error: "internal_error",
      message: lastError,
    });
  }
});

server.listen(listenPort, listenHost, () => {
  console.log(
    `[joy-bridge] listening on http://${listenHost}:${listenPort} db=${dbPath}`,
  );
});

async function shutdown(signal) {
  console.log(`[joy-bridge] ${signal} — flushing and stopping`);
  await new Promise((resolve) => server.close(resolve));
  try {
    await dbPool.stop();
  } catch (err) {
    console.error("[joy-bridge] shutdown error", err);
  }
  process.exit(0);
}

process.on("SIGINT", () => shutdown("SIGINT"));
process.on("SIGTERM", () => shutdown("SIGTERM"));
