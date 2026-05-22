using JoyZoning.Domain.Agents;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Tests.Infrastructure;

/// <summary>Deterministic DietCode adapter for API integration tests (no Hermes HTTP).</summary>
public sealed class TestDietCodeAdapter : IAgentAdapter
{
    public bool FailNextDispatch { get; set; }

    public AgentKind Kind => AgentKind.DietCode;

    public Task<HealthStatus> GetHealthAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new HealthStatus(HealthState.Healthy, "test"));

    public Task<string> StartRunAsync(AgentRunRequest request, CancellationToken cancellationToken = default)
    {
        if (FailNextDispatch)
        {
            FailNextDispatch = false;
            throw new InvalidOperationException("Simulated Hermes dispatch failure");
        }

        return Task.FromResult($"test-run-{Guid.NewGuid():N}");
    }

    public Task StopRunAsync(string runId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public async IAsyncEnumerable<NormalizedAgentEvent> StreamEventsAsync(
        string runId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public Task ResolveApprovalAsync(
        string runId,
        ApprovalResolution resolution,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public sealed class TestHermesAdapter : IAgentAdapter
{
    public AgentKind Kind => AgentKind.Hermes;

    public Task<HealthStatus> GetHealthAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new HealthStatus(HealthState.Healthy, "test"));

    public Task<string> StartRunAsync(AgentRunRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult($"manager-run-{Guid.NewGuid():N}");

    public Task StopRunAsync(string runId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public async IAsyncEnumerable<NormalizedAgentEvent> StreamEventsAsync(
        string runId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public Task ResolveApprovalAsync(
        string runId,
        ApprovalResolution resolution,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
