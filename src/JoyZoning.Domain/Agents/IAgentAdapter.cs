using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Agents;

public interface IAgentAdapter
{
    AgentKind Kind { get; }

    Task<HealthStatus> GetHealthAsync(CancellationToken cancellationToken = default);

    Task<string> StartRunAsync(AgentRunRequest request, CancellationToken cancellationToken = default);

    Task<AgentRunStartResult> StartRunDetailedAsync(
        AgentRunRequest request,
        CancellationToken cancellationToken = default);

    Task StopRunAsync(string runId, CancellationToken cancellationToken = default);

    Task<AgentRunPollResult?> PollRunStatusAsync(string runId, CancellationToken cancellationToken = default);

    IAsyncEnumerable<NormalizedAgentEvent> StreamEventsAsync(
        string runId,
        CancellationToken cancellationToken = default);

    Task ResolveApprovalAsync(
        string runId,
        ApprovalResolution resolution,
        CancellationToken cancellationToken = default);
}

public sealed record AgentRunRequest
{
    public string Prompt { get; init; } = string.Empty;
    public string? SessionId { get; init; }
    public string WorkspaceRoot { get; init; } = string.Empty;
    public IReadOnlyList<string>? EnabledToolsets { get; init; }
    public IReadOnlyDictionary<string, string>? Environment { get; init; }
    public AgentRole Role { get; init; } = AgentRole.Executor;
}

public sealed record HealthStatus(HealthState State, string Message);

public sealed record NormalizedAgentEvent(
    string EventType,
    string PayloadJson,
    DateTimeOffset OccurredAt);

public sealed record ApprovalResolution(
    ApprovalScope Scope,
    bool ResolveAll = false,
    string? ModifiedParametersJson = null);
