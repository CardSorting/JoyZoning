/**
 * @deprecated Removed — LegacyRuntimeShim client (:9090) is no longer supported.
 * Use JoyZoning control plane `/api/hermes/*` and external Hermes InstallRoot.
 */

const DEPRECATION_MESSAGE =
  '@joyzoning/agent-bridge is removed. Use JoyZoning control plane /api/hermes/health and external Hermes (Hermes:InstallRoot). See docs/architecture/hermes-runtime-reversal.md.';

function reject(): never {
  throw new Error(DEPRECATION_MESSAGE);
}

/** @deprecated */
export class AgentBridgeClient {
  constructor(_baseUrl?: string) {
    reject();
  }

  async startTask(): Promise<never> {
    return reject();
  }

  async cancelTask(): Promise<never> {
    return reject();
  }

  async getStatus(): Promise<never> {
    return reject();
  }

  async listEvents(): Promise<never> {
    return reject();
  }

  async approveAction(): Promise<never> {
    return reject();
  }

  async rejectAction(): Promise<never> {
    return reject();
  }

  async getRuntimeHealth(): Promise<never> {
    return reject();
  }
}
