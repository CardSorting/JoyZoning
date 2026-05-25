/**
 * LegacyRuntimeShim tombstone — HTTP 410 on :9090.
 * Canonical Hermes runs from Hermes:InstallRoot (external checkout), not this package.
 */
import http from 'node:http';

const PORT = Number(process.env.LEGACY_RUNTIME_SHIM_PORT || 9090);

const DEPRECATION = {
  status: 'deprecated',
  legacyRuntimeShim: true,
  httpStatus: 410,
  message:
    'Vendored Hermes under apps/agent-runtime was removed. Point JoyZoning Hermes:InstallRoot at your external diet-hermes checkout.',
  docs: 'docs/architecture/hermes-runtime-reversal.md',
  canonicalHealth: 'http://127.0.0.1:9470/api/hermes/health',
} as const;

function sendJson(res: http.ServerResponse, status: number, body: unknown) {
  const payload = JSON.stringify(body);
  res.writeHead(status, {
    'Content-Type': 'application/json',
    'Content-Length': Buffer.byteLength(payload),
    'X-Legacy-Runtime-Shim': 'removed',
  });
  res.end(payload);
}

const server = http.createServer((req, res) => {
  if (req.method !== 'GET' && req.method !== 'HEAD') {
    sendJson(res, 405, { ...DEPRECATION, error: 'method_not_allowed' });
    return;
  }
  if (req.method === 'HEAD') {
    res.writeHead(410, { 'X-Legacy-Runtime-Shim': 'removed' });
    res.end();
    return;
  }
  const path = (req.url || '/').split('?')[0];
  if (path === '/health' || path === '/') {
    sendJson(res, 410, DEPRECATION);
    return;
  }
  sendJson(res, 410, { ...DEPRECATION, path, hint: 'All LegacyRuntimeShim routes are gone.' });
});

server.listen(PORT, '127.0.0.1', () => {
  console.log(
    `[LegacyRuntimeShim] Tombstone listening on http://127.0.0.1:${PORT} (410 — use external Hermes)`
  );
});
