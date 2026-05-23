namespace JoyZoning.Domain.Agents;

/// <summary>Hermes API run status from GET /v1/runs/{id}.</summary>
public sealed record AgentRunPollResult(string RunId, string Status, bool IsTerminal);
