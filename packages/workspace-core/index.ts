import * as path from 'node:path';
import * as fs from 'node:fs';

export class WorkspaceModel {
  public readonly rootPath: string;

  constructor(rootPath?: string) {
    this.rootPath = rootPath || process.env.JOY_WORKSPACE_ROOT || path.resolve(process.cwd(), '.joy-workspaces/default');
  }

  getReposDir() { return path.join(this.rootPath, 'repos'); }
  getSessionsDir() { return path.join(this.rootPath, 'sessions'); }
  getLogsDir() { return path.join(this.rootPath, 'logs'); }
  getAgentStateDir() { return path.join(this.rootPath, 'agent-state'); }
  getApprovalsDir() { return path.join(this.rootPath, 'approvals'); }
  getArtifactsDir() { return path.join(this.rootPath, 'artifacts'); }

  initialize() {
    fs.mkdirSync(this.rootPath, { recursive: true });
    fs.mkdirSync(this.getReposDir(), { recursive: true });
    fs.mkdirSync(this.getSessionsDir(), { recursive: true });
    fs.mkdirSync(this.getLogsDir(), { recursive: true });
    fs.mkdirSync(this.getAgentStateDir(), { recursive: true });
    fs.mkdirSync(this.getApprovalsDir(), { recursive: true });
    fs.mkdirSync(this.getArtifactsDir(), { recursive: true });
  }

  validatePath(targetPath: string): boolean {
    const resolvedTarget = path.resolve(targetPath);
    const resolvedRoot = path.resolve(this.rootPath);
    return resolvedTarget === resolvedRoot || resolvedTarget.startsWith(resolvedRoot + path.sep);
  }
}
