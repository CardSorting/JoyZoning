using JoyZoning.Domain.Agents;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Agents.Hermes;

/// <summary>
/// DietCode execution agent — bounded Hermes run with executor toolsets.
/// </summary>
public class DietCodeAdapter : IAgentAdapter
{
    private static readonly string[] ExecutorToolsets =
    {
        "terminal", "file", "search", "patch",
    };

    private readonly HermesHttpClient _client;

    public DietCodeAdapter(HermesHttpClient client) => _client = client;

    public AgentKind Kind => AgentKind.DietCode;

    public Task<HealthStatus> GetHealthAsync(CancellationToken cancellationToken = default) =>
        _client.GetHealthAsync(cancellationToken);

    public async Task<string> StartRunAsync(AgentRunRequest request, CancellationToken cancellationToken = default)
    {
        var started = await StartRunDetailedAsync(request, cancellationToken);
        return started.RunId;
    }

    public Task<AgentRunStartResult> StartRunDetailedAsync(
        AgentRunRequest request,
        CancellationToken cancellationToken = default)
    {
        var preamble =
            "You are DietCode, a bounded software execution agent. " +
            "Report progress clearly. Request approval before risky operations. " +
            "Do not delegate to subagents.\n\n";

        var executorRequest = new AgentRunRequest
        {
            Prompt = preamble + request.Prompt,
            SessionId = request.SessionId,
            WorkspaceRoot = request.WorkspaceRoot,
            Role = AgentRole.Executor,
            EnabledToolsets = request.EnabledToolsets ?? ExecutorToolsets,
            Environment = new Dictionary<string, string>(request.Environment ?? new Dictionary<string, string>())
            {
                ["TERMINAL_CWD"] = request.WorkspaceRoot,
            },
        };

        return _client.StartRunDetailedAsync(executorRequest, cancellationToken);
    }

    public Task StopRunAsync(string runId, CancellationToken cancellationToken = default) =>
        _client.StopRunAsync(runId, cancellationToken);

    public Task<AgentRunPollResult?> PollRunStatusAsync(string runId, CancellationToken cancellationToken = default) =>
        _client.PollRunStatusAsync(runId, cancellationToken);

    public IAsyncEnumerable<NormalizedAgentEvent> StreamEventsAsync(
        string runId,
        CancellationToken cancellationToken = default) =>
        _client.StreamEventsAsync(runId, cancellationToken);

    public Task ResolveApprovalAsync(
        string runId,
        ApprovalResolution resolution,
        CancellationToken cancellationToken = default) =>
        _client.ResolveApprovalAsync(runId, resolution, cancellationToken);
}
