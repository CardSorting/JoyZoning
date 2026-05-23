import express from 'express';
import cors from 'cors';
import { spawn, ChildProcess } from 'node:child_process';
import * as path from 'node:path';
import * as fs from 'node:fs';
import { WorkspaceModel } from '@joyzoning/workspace-core';
import { RuntimeHealth, AgentTaskStatus, AgentEvent } from '@joyzoning/shared-contracts';

const app = express();
app.use(cors());
app.use(express.json());

const PORT = 9090;
const PYTHON_GATEWAY_PORT = 8642;

// Initialize Workspace Model
const workspace = new WorkspaceModel();
workspace.initialize();

const allowedWorkspaceRoot = workspace.rootPath;
let gatewayProcess: ChildProcess | null = null;
const eventHistory: AgentEvent[] = [];

// Start the Python gateway process under the hood
function startPythonGateway() {
  const agentRuntimeDir = __dirname;
  const isWindows = process.platform === 'win32';
  
  // Resolve python executable in .venv or venv
  let pythonPath = 'python3';
  const venvPaths = [
    path.join(agentRuntimeDir, '.venv', isWindows ? 'Scripts' : 'bin', isWindows ? 'python.exe' : 'python'),
    path.join(agentRuntimeDir, 'venv', isWindows ? 'Scripts' : 'bin', isWindows ? 'python.exe' : 'python')
  ];

  for (const p of venvPaths) {
    if (fs.existsSync(p)) {
      pythonPath = p;
      break;
    }
  }

  console.log(`[agent-runtime-server] Starting Python gateway using: ${pythonPath}`);

  // We run python run_agent.py / cli.py as a gateway
  const cliScript = path.join(agentRuntimeDir, 'cli.py');
  const args = [cliScript, '-p', 'joyzoning', 'gateway'];

  const env = {
    ...process.env,
    HERMES_WRITE_SAFE_ROOT: allowedWorkspaceRoot,
    JOY_WORKSPACE_ROOT: allowedWorkspaceRoot,
    API_SERVER_ENABLED: '1',
    API_SERVER_HOST: '127.0.0.1',
    API_SERVER_PORT: String(PYTHON_GATEWAY_PORT)
  };

  gatewayProcess = spawn(pythonPath, args, {
    cwd: agentRuntimeDir,
    env,
    stdio: 'inherit'
  });

  gatewayProcess.on('exit', (code) => {
    console.log(`[agent-runtime-server] Python gateway exited with code ${code}`);
    gatewayProcess = null;
  });
}

// Check containment status file
function getContainmentStatus() {
  const statusFile = path.join(allowedWorkspaceRoot, 'agent-state', 'containment-status.json');
  if (fs.existsSync(statusFile)) {
    try {
      return JSON.parse(fs.readFileSync(statusFile, 'utf8'));
    } catch {
      // fallback
    }
  }
  return {
    strictMode: true,
    blockedWritesCount: 0
  };
}

// Express Endpoint Implementations

// GET /health
app.get('/health', async (req, res) => {
  let isHealthy = false;
  let message = 'Python gateway is offline';

  try {
    const response = await fetch(`http://127.0.0.1:${PYTHON_GATEWAY_PORT}/health`);
    if (response.ok) {
      isHealthy = true;
      message = 'Agent runtime is healthy';
    }
  } catch (err) {
    // offline
  }

  const containment = getContainmentStatus();

  const health: RuntimeHealth = {
    status: isHealthy ? 'healthy' : 'unhealthy',
    message,
    uptimeMs: process.uptime() * 1000,
    allowedWorkspaceRoot,
    containmentStatus: {
      strictMode: true,
      blockedWritesCount: containment.blockedWritesCount || 0,
      lastBlockedAttempt: containment.lastBlockedAttempt
    }
  };

  res.json(health);
});

// POST /tasks
app.post('/tasks', async (req, res) => {
  const { taskId, title, objective, initialContext, priority } = req.body;
  if (!taskId || !objective) {
    return res.status(400).json({ error: 'taskId and objective are required' });
  }

  const body = {
    input: objective,
    session_id: taskId,
    metadata: {
      workspace_root: allowedWorkspaceRoot,
      role: 'executor',
      title,
      priority: priority || 0,
      initial_context: initialContext || ''
    }
  };

  try {
    const response = await fetch(`http://127.0.0.1:${PYTHON_GATEWAY_PORT}/v1/runs`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(body)
    });

    if (!response.ok) {
      const errText = await response.text();
      return res.status(response.status).json({ error: 'Failed to start task in gateway', detail: errText });
    }

    const data = await response.json();
    const runId = data.run_id || data.id;

    // Log start task event
    const event: AgentEvent = {
      id: Math.random().toString(36).substring(2),
      taskId,
      type: 'info',
      message: `Task initiated: ${title}`,
      timestamp: Date.now(),
      payloadJson: JSON.stringify(data)
    };
    eventHistory.push(event);

    const status: AgentTaskStatus = {
      taskId,
      status: 'running',
      createdAt: Date.now(),
      updatedAt: Date.now(),
      startedAt: Date.now()
    };
    res.json(status);
  } catch (err: any) {
    res.status(500).json({ error: 'Internal gateway connection error', message: err.message });
  }
});

// GET /tasks/:id
app.get('/tasks/:id', async (req, res) => {
  const taskId = req.params.id;
  try {
    const response = await fetch(`http://127.0.0.1:${PYTHON_GATEWAY_PORT}/v1/runs/${taskId}`);
    if (!response.ok) {
      return res.status(response.status).json({ error: `Failed to fetch status for run ${taskId}` });
    }
    const data = await response.json();
    // Map status string to state enum
    let mappedState: any = 'pending';
    if (data.status === 'completed') mappedState = 'completed';
    else if (data.status === 'failed') mappedState = 'failed';
    else if (data.status === 'cancelled' || data.status === 'stopped') mappedState = 'cancelled';
    else if (data.status === 'running') mappedState = 'running';

    const status: AgentTaskStatus = {
      taskId,
      status: mappedState,
      result: data.result || undefined,
      createdAt: Date.now(),
      updatedAt: Date.now()
    };
    res.json(status);
  } catch (err: any) {
    res.status(500).json({ error: 'Internal gateway connection error', message: err.message });
  }
});

// POST /tasks/:id/cancel
app.post('/tasks/:id/cancel', async (req, res) => {
  const taskId = req.params.id;
  try {
    const response = await fetch(`http://127.0.0.1:${PYTHON_GATEWAY_PORT}/v1/runs/${taskId}/stop`, {
      method: 'POST'
    });
    if (!response.ok) {
      return res.status(response.status).json({ error: `Failed to cancel task ${taskId}` });
    }
    res.json({ success: true });
  } catch (err: any) {
    res.status(500).json({ error: 'Internal gateway connection error', message: err.message });
  }
});

// GET /events
app.get('/events', (req, res) => {
  const taskId = req.query.taskId as string;
  if (taskId) {
    res.json(eventHistory.filter(e => e.taskId === taskId));
  } else {
    res.json(eventHistory);
  }
});

// POST /approvals/:id/approve
app.post('/approvals/:id/approve', async (req, res) => {
  const runId = req.params.id;
  try {
    const response = await fetch(`http://127.0.0.1:${PYTHON_GATEWAY_PORT}/v1/runs/${runId}/approval`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ choice: 'once' })
    });
    if (!response.ok) {
      return res.status(response.status).json({ error: `Failed to approve run ${runId}` });
    }
    res.json({ success: true });
  } catch (err: any) {
    res.status(500).json({ error: 'Internal gateway connection error', message: err.message });
  }
});

// POST /approvals/:id/reject
app.post('/approvals/:id/reject', async (req, res) => {
  const runId = req.params.id;
  try {
    const response = await fetch(`http://127.0.0.1:${PYTHON_GATEWAY_PORT}/v1/runs/${runId}/approval`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ choice: 'deny' })
    });
    if (!response.ok) {
      return res.status(response.status).json({ error: `Failed to reject run ${runId}` });
    }
    res.json({ success: true });
  } catch (err: any) {
    res.status(500).json({ error: 'Internal gateway connection error', message: err.message });
  }
});

// Start API Server
app.listen(PORT, () => {
  console.log(`[agent-runtime-server] Local API server listening on http://127.0.0.1:${PORT}`);
  startPythonGateway();
});

// Cleanup processes on exit
process.on('SIGINT', () => {
  if (gatewayProcess) {
    console.log('[agent-runtime-server] Cleaning up child python processes...');
    gatewayProcess.kill('SIGINT');
  }
  process.exit(0);
});

process.on('SIGTERM', () => {
  if (gatewayProcess) {
    console.log('[agent-runtime-server] Cleaning up child python processes...');
    gatewayProcess.kill('SIGTERM');
  }
  process.exit(0);
});
