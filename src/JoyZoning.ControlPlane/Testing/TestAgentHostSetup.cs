using JoyZoning.Agents;
using JoyZoning.Agents.Hermes;
using JoyZoning.Domain.Agents;
using JoyZoning.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace JoyZoning.ControlPlane.Testing;

/// <summary>Deterministic agent adapters for ASPNETCORE_ENVIRONMENT=Testing (dogfood + integration).</summary>
public static class TestAgentHostSetup
{
    public static void ReplaceAgents(IServiceCollection services)
    {
        services.RemoveAll<IHostedService>();

        services.RemoveAll<HermesAdapter>();
        services.RemoveAll<DietCodeAdapter>();
        services.RemoveAll<IAgentAdapter>();
        services.RemoveAll<AgentAdapterRegistry>();

        services.AddSingleton<IAgentAdapter, StubHermesAgentAdapter>();
        services.AddSingleton<IAgentAdapter, StubDietCodeAgentAdapter>();
        services.AddSingleton<AgentAdapterRegistry>(sp =>
            new AgentAdapterRegistry(sp.GetServices<IAgentAdapter>()));
    }
}

internal sealed class StubDietCodeAgentAdapter : IAgentAdapter
{
    public static bool FailNextDispatch { get; set; }

    public AgentKind Kind => AgentKind.DietCode;

    public Task<HealthStatus> GetHealthAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new HealthStatus(HealthState.Healthy, "test"));

    public Task<string> StartRunAsync(AgentRunRequest request, CancellationToken cancellationToken = default)
    {
        if (FailNextDispatch)
        {
            FailNextDispatch = false;
            throw new InvalidOperationException("Simulated dispatch failure");
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

internal sealed class StubHermesAgentAdapter : IAgentAdapter
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
