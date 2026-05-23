import {
  AgentTaskRequest,
  AgentTaskStatus,
  AgentEvent,
  ApprovalRequest,
  RuntimeHealth
} from '@joyzoning/shared-contracts';

export class AgentBridgeClient {
  private baseUrl: string;

  constructor(baseUrl = 'http://127.0.0.1:9090') {
    this.baseUrl = baseUrl;
  }

  async startTask(request: AgentTaskRequest): Promise<AgentTaskStatus> {
    const res = await fetch(`${this.baseUrl}/tasks`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request)
    });
    if (!res.ok) throw new Error(`startTask failed: ${res.statusText}`);
    return res.json();
  }

  async cancelTask(taskId: string): Promise<boolean> {
    const res = await fetch(`${this.baseUrl}/tasks/${taskId}/cancel`, {
      method: 'POST'
    });
    if (!res.ok) throw new Error(`cancelTask failed: ${res.statusText}`);
    const data = await res.json();
    return data.success;
  }

  async getStatus(taskId: string): Promise<AgentTaskStatus> {
    const res = await fetch(`${this.baseUrl}/tasks/${taskId}`);
    if (!res.ok) throw new Error(`getStatus failed: ${res.statusText}`);
    return res.json();
  }

  async listEvents(taskId?: string): Promise<AgentEvent[]> {
    const url = taskId ? `${this.baseUrl}/events?taskId=${taskId}` : `${this.baseUrl}/events`;
    const res = await fetch(url);
    if (!res.ok) throw new Error(`listEvents failed: ${res.statusText}`);
    return res.json();
  }

  async approveAction(approvalId: string): Promise<boolean> {
    const res = await fetch(`${this.baseUrl}/approvals/${approvalId}/approve`, {
      method: 'POST'
    });
    if (!res.ok) throw new Error(`approveAction failed: ${res.statusText}`);
    const data = await res.json();
    return data.success;
  }

  async rejectAction(approvalId: string): Promise<boolean> {
    const res = await fetch(`${this.baseUrl}/approvals/${approvalId}/reject`, {
      method: 'POST'
    });
    if (!res.ok) throw new Error(`rejectAction failed: ${res.statusText}`);
    const data = await res.json();
    return data.success;
  }

  async getRuntimeHealth(): Promise<RuntimeHealth> {
    const res = await fetch(`${this.baseUrl}/health`);
    if (!res.ok) throw new Error(`getRuntimeHealth failed: ${res.statusText}`);
    return res.json();
  }
}
