import { spawn, ChildProcess } from 'node:child_process';
import * as path from 'node:path';

const ROOT_DIR = path.resolve(__dirname, '..');

// Helper colors for prefixed logging
const PRETTY_NAMES: Record<string, { prefix: string; color: string }> = {
  controlPlane: { prefix: '[ControlPlane]', color: '\x1b[36m' }, // Cyan
  agentRuntime: { prefix: '[LegacyRuntimeShim]', color: '\x1b[32m' }, // dev-only, not started by default
  watchUi:      { prefix: '[WatchUI]     ', color: '\x1b[33m' }, // Yellow
  desktopApp:   { prefix: '[DesktopApp]  ', color: '\x1b[35m' }  // Magenta
};

const RESET = '\x1b[0m';

const children: { name: string; process: ChildProcess }[] = [];
let isCleaningUp = false;

function logLine(name: string, data: string) {
  const cfg = PRETTY_NAMES[name] || { prefix: `[${name}]`, color: '' };
  const lines = data.split('\n');
  for (const line of lines) {
    if (line.trim()) {
      console.log(`${cfg.color}${cfg.prefix}${RESET} ${line}`);
    }
  }
}

function spawnProcess(name: string, command: string, args: string[]) {
  logLine(name, `Starting: ${command} ${args.join(' ')}`);
  
  const child = spawn(command, args, {
    cwd: ROOT_DIR,
    shell: true,
    env: {
      ...process.env,
      // Pass the workspace root so that the C# and Python environments align
      JOY_WORKSPACE_ROOT: path.join(ROOT_DIR, '.joy-workspaces', 'default')
    }
  });

  children.push({ name, process: child });

  child.stdout?.on('data', (data: any) => {
    logLine(name, data.toString());
  });

  child.stderr?.on('data', (data: any) => {
    logLine(name, `ERR: ${data.toString()}`);
  });

  child.on('exit', (code: number | null) => {
    if (!isCleaningUp) {
      logLine(name, `Process exited with code ${code}`);
      cleanup(code || 0);
    }
  });
}

function cleanup(exitCode: number) {
  if (isCleaningUp) return;
  isCleaningUp = true;

  console.log('\n\x1b[31mCleaning up child processes...\x1b[0m');

  for (const child of children) {
    try {
      if (child.process.pid && !child.process.killed) {
        logLine(child.name, `Killing process ${child.process.pid}...`);
        child.process.kill('SIGTERM');
      }
    } catch (err) {
      // ignore
    }
  }

  process.exit(exitCode);
}

// Register exit handlers
process.on('SIGINT', () => cleanup(0));
process.on('SIGTERM', () => cleanup(0));
process.on('uncaughtException', (err: any) => {
  console.error('\x1b[31mUncaught Exception:\x1b[0m', err);
  cleanup(1);
});

async function main() {
  console.log('\x1b[36m⚕ Starting JoyZoning Stack Concurrently...\x1b[0m\n');

  // 1. Control Plane C# Server
  spawnProcess('controlPlane', 'dotnet', ['run', '--project', 'src/JoyZoning.ControlPlane']);

  // 2. LegacyRuntimeShim (dev-only — not canonical Hermes; use external InstallRoot)
  // spawnProcess('legacyRuntimeShim', 'pnpm', ['--filter', '@joyzoning/agent-runtime', 'dev']);

  // 3. JoyZoning Watch Next.js UI
  spawnProcess('watchUi', 'pnpm', ['--filter', '@joyzoning/watch', 'dev']);

  // 4. JoyZoning Desktop Application Client
  // Give the control plane and API server a brief moment to boot up first
  setTimeout(() => {
    if (!isCleaningUp) {
      spawnProcess('desktopApp', 'dotnet', ['run', '--project', 'src/JoyZoning.App']);
    }
  }, 2000);
}

main().catch((err: any) => {
  console.error('\x1b[31mExecution failed:\x1b[0m', err);
  cleanup(1);
});
