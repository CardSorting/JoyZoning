using JoyZoning.ControlPlane.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace JoyZoning.Tests.Infrastructure;

/// <summary>No-op SignalR hub for integration tests (avoids protocol/token errors in WebApplicationFactory).</summary>
internal sealed class TestOperatorHubContext : IHubContext<OperatorHub>
{
    public IHubClients Clients { get; } = new TestHubClients();
    public IGroupManager Groups => throw new NotSupportedException("Groups not used in API integration tests.");
}

internal sealed class TestHubClients : IHubClients
{
    private static readonly TestClientProxy Proxy = new();

    public IClientProxy All => Proxy;
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy;
    public IClientProxy Client(string connectionId) => Proxy;
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy;
    public IClientProxy Group(string groupName) => Proxy;
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy;
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy;
    public IClientProxy User(string userId) => Proxy;
    public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy;
}

internal sealed class TestClientProxy : IClientProxy
{
    public Task SendCoreAsync(string methodName, object?[] args, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
