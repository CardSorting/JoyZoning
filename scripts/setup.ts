import * as fs from 'node:fs';
import * as path from 'node:path';
import { spawnSync, execSync } from 'node:child_process';
import * as net from 'node:net';

const ROOT_DIR = path.resolve(__dirname, '..');

// Helper colors
const GREEN = '\x1b[32m';
const YELLOW = '\x1b[33m';
const RED = '\x1b[31m';
const CYAN = '\x1b[36m';
const NC = '\x1b[0m';

console.log(`${CYAN}⚕ JoyZoning Habitat Setup Wizard${NC}\n`);

// 1. Check Node.js and package manager versions
function checkVersions() {
  console.log(`${CYAN}→${NC} Checking runtime and SDK versions...`);
  
  // Node.js
  const nodeVersion = process.version;
  console.log(`  Node.js: ${GREEN}${nodeVersion}${NC}`);
  
  // pnpm
  try {
    const pnpmVersion = execSync('pnpm --version', { encoding: 'utf8' }).trim();
    console.log(`  pnpm: ${GREEN}v${pnpmVersion}${NC}`);
  } catch (err) {
    console.log(`  pnpm: ${RED}Not found! Please install pnpm globally.${NC}`);
    process.exit(1);
  }

  // dotnet SDK
  try {
    const dotnetVersion = execSync('dotnet --version', { encoding: 'utf8' }).trim();
    if (dotnetVersion.startsWith('8.')) {
      console.log(`  .NET SDK: ${GREEN}v${dotnetVersion}${NC}`);
    } else {
      console.log(`  .NET SDK: ${YELLOW}v${dotnetVersion} (Warning: Expected v8.x)${NC}`);
    }
  } catch (err) {
    console.log(`  .NET SDK: ${RED}Not found! Please install .NET 8.0 SDK.${NC}`);
    process.exit(1);
  }
}

// 2. Validate Port Availability
async function checkPort(port: number): Promise<boolean> {
  return new Promise((resolve) => {
    const server = net.createServer();
    server.once('error', (err: any) => {
      if (err.code === 'EADDRINUSE') {
        resolve(false);
      } else {
        resolve(true); // Other errors might not mean port is busy
      }
    });
    server.once('listening', () => {
      server.close();
      resolve(true);
    });
    server.listen(port, '127.0.0.1');
  });
}

async function validatePorts() {
  console.log(`${CYAN}→${NC} Validating port availability...`);
  const ports = [
    { port: 9470, name: 'JoyZoning Control Plane Server' },
    { port: 3000, name: 'JoyZoning Watch Next.js UI' },
  ];

  for (const { port, name } of ports) {
    const available = await checkPort(port);
    if (!available) {
      console.log(`  ${RED}✗ Port ${port} (${name}) is already in use!${NC}`);
      console.log(`    Please stop the process using port ${port} and try again.`);
      process.exit(1);
    } else {
      console.log(`  ${GREEN}✓ Port ${port} (${name})${NC} is available.`);
    }
  }
}

// 3. Create env files from examples
function setupEnvFiles() {
  console.log(`${CYAN}→${NC} Setting up environment files...`);
  const envConfigs = [
    {
      example: path.join(ROOT_DIR, '.env.example'),
      target: path.join(ROOT_DIR, '.env'),
    },
  ];

  for (const { example, target } of envConfigs) {
    if (!fs.existsSync(target)) {
      if (fs.existsSync(example)) {
        fs.copyFileSync(example, target);
        console.log(`  ${GREEN}✓${NC} Created: ${path.relative(ROOT_DIR, target)}`);
      } else {
        console.log(`  ${YELLOW}⚠${NC} Example env not found: ${path.relative(ROOT_DIR, example)}`);
      }
    } else {
      console.log(`  ${GREEN}✓${NC} File exists: ${path.relative(ROOT_DIR, target)}`);
    }
  }
}

// 4. Initialize local workspace directories
function initWorkspaceDirectories() {
  console.log(`${CYAN}→${NC} Initializing workspace folders...`);
  const workspaceRoot = path.join(ROOT_DIR, '.joy-workspaces', 'default');
  const dirs = [
    workspaceRoot,
    path.join(workspaceRoot, 'repos'),
    path.join(workspaceRoot, 'sessions'),
    path.join(workspaceRoot, 'logs'),
    path.join(workspaceRoot, 'agent-state'),
    path.join(workspaceRoot, 'approvals'),
    path.join(workspaceRoot, 'artifacts')
  ];

  for (const dir of dirs) {
    if (!fs.existsSync(dir)) {
      fs.mkdirSync(dir, { recursive: true });
      console.log(`  ${GREEN}✓${NC} Created folder: ${path.relative(ROOT_DIR, dir)}`);
    } else {
      console.log(`  ${GREEN}✓${NC} Folder exists: ${path.relative(ROOT_DIR, dir)}`);
    }
  }

  // Create initial containment status file if it doesn't exist
  const statusFile = path.join(workspaceRoot, 'agent-state', 'containment-status.json');
  if (!fs.existsSync(statusFile)) {
    fs.writeFileSync(statusFile, JSON.stringify({
      strictMode: true,
      blockedWritesCount: 0
    }, null, 2));
    console.log(`  ${GREEN}✓${NC} Initialized containment-status.json`);
  }
}

// 5. Build/compile packages and apps
function buildPackages() {
  console.log(`${CYAN}→${NC} Building workspace packages...`);
  
  // Tombstone packages only — legacy bridge/workspace/contracts have no runtime consumers.
  const projects = [
    { name: '@joyzoning/agent-runtime', dir: 'apps/agent-runtime' },
  ];

  for (const proj of projects) {
    console.log(`  Building ${proj.name}...`);
    const res = spawnSync('pnpm', ['--filter', proj.name, 'build'], {
      cwd: ROOT_DIR,
      stdio: 'inherit'
    });
    if (res.status !== 0) {
      console.log(`  ${RED}✗ Failed to build ${proj.name}!${NC}`);
      process.exit(1);
    }
  }
  console.log(`  ${GREEN}✓ All TypeScript projects built successfully.${NC}`);
}

function printHermesReminder() {
  console.log(`${CYAN}→${NC} External Hermes (runtime) is not installed by this wizard.`);
  console.log(`  Set HERMES_INSTALL_ROOT in .env to your diet-hermes checkout.`);
  console.log(`  Run ${CYAN}hermes setup${NC} there; configure joyzoning.control_plane in ~/.hermes/config.yaml.`);
  console.log(`  See docs/architecture/hermes-runtime-reversal.md`);
}

async function main() {
  checkVersions();
  await validatePorts();
  setupEnvFiles();
  initWorkspaceDirectories();
  buildPackages();
  printHermesReminder();

  console.log(`\n${GREEN}✓ JoyZoning habitat setup complete!${NC}\n`);
  console.log('Next Steps:');
  console.log('  1. Configure external Hermes (~/.hermes/.env + config.yaml)');
  console.log('  2. Set HERMES_INSTALL_ROOT in JoyZoning .env / control plane settings');
  console.log('  3. Run the habitat stack:');
  console.log(`     ${CYAN}pnpm dev${NC}\n`);
}

main().catch((err) => {
  console.error(`${RED}Setup failed with error:${NC}`, err);
  process.exit(1);
});
