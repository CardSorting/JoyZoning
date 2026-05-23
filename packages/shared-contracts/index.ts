export interface AgentTaskRequest {
  taskId: string;
  title: string;
  objective: string;
  initialContext?: string;
  priority?: number;
}

export type AgentTaskStatusState = 'pending' | 'running' | 'completed' | 'failed' | 'cancelled';

export interface AgentTaskStatus {
  taskId: string;
  status: AgentTaskStatusState;
  result?: string;
  createdAt: number;
  updatedAt: number;
  startedAt?: number;
  completedAt?: number;
  error?: string;
}

export interface AgentEvent {
  id: string;
  taskId: string;
  type: 'info' | 'success' | 'warn' | 'error';
  message: string;
  timestamp: number;
  payloadJson?: string;
}

export interface ApprovalRequest {
  id: string;
  taskId: string;
  command: string;
  description: string;
  status: 'pending' | 'approved' | 'rejected';
  timestamp: number;
}

export interface WorkspaceSession {
  sessionId: string;
  workspaceRoot: string;
  createdAt: number;
  updatedAt: number;
}

export interface RuntimeHealth {
  status: 'healthy' | 'unhealthy';
  message?: string;
  version?: string;
  uptimeMs: number;
  allowedWorkspaceRoot: string;
  containmentStatus: {
    strictMode: boolean;
    blockedWritesCount: number;
    lastBlockedAttempt?: {
      path: string;
      timestamp: number;
    };
  };
}

export interface VerificationResult {
  taskId: string;
  success: boolean;
  output: string;
  timestamp: number;
}
