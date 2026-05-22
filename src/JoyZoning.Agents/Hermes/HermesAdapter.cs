using JoyZoning.Domain.Agents;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Agents.Hermes;

public class HermesAdapter : IAgentAdapter
{
    private readonly HermesHttpClient _client;

    public HermesAdapter(HermesHttpClient client) => _client = client;

    public AgentKind Kind => AgentKind.Hermes;

    public Task<HealthStatus> GetHealthAsync(CancellationToken cancellationToken = default) =>
        _client.GetHealthAsync(cancellationToken);

    public Task<string> StartRunAsync(AgentRunRequest request, CancellationToken cancellationToken = default)
    {
        var managerRequest = request with
        {
            Role = AgentRole.Manager,
            EnabledToolsets = request.EnabledToolsets ?? new[]
            {
                "kanban", "delegation", "clarify", "todo", "session_search",
            },
        };
        return _client.StartRunAsync(managerRequest, cancellationToken);
    }

    public Task StopRunAsync(string runId, CancellationToken cancellationToken = default) =>
        _client.StopRunAsync(runId, cancellationToken);

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
