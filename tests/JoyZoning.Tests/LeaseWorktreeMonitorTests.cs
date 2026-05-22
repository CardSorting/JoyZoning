using JoyZoning.ControlPlane.Services;
using JoyZoning.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JoyZoning.Tests;

[Collection("OrchestrationApi")]
public class LeaseWorktreeMonitorTests : IClassFixture<JoyZoningApiFixture>, IAsyncLifetime
{
    private readonly JoyZoningApiFixture _fixture;

    public LeaseWorktreeMonitorTests(JoyZoningApiFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ScanActiveLeases_publishes_when_worktree_changes()
    {
        WorkspaceEventPublisher.ClearDedupeCacheForTests();

        var api = new OrchestrationApiClient(_fixture.Client);
        var root = Path.Combine(Path.GetTempPath(), "jz-monitor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var (_, taskId) = await api.SeedTaskAsync(root);
            await api.DispatchAsync(taskId);

            using var scope = _fixture.Factory.Services.CreateScope();
            var monitor = scope.ServiceProvider.GetRequiredService<LeaseWorktreeMonitor>();

            var first = await monitor.ScanActiveLeasesAsync();
            Assert.True(first >= 0);

            await File.WriteAllTextAsync(Path.Combine(
                (await ReadWorktreeAsync(api, taskId))!, "monitor-dirty.txt"), "x");

            WorkspaceEventPublisher.ClearDedupeCacheForTests();
            var second = await monitor.ScanActiveLeasesAsync();
            Assert.True(second >= 1);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignore */ }
        }
    }

    private static async Task<string?> ReadWorktreeAsync(OrchestrationApiClient api, Guid taskId)
    {
        var (_, lease, _, _) = await OrchestrationApiClient.ReadAsync(await api.GetLeaseAsync(taskId));
        return lease?.GetProperty("worktreePath").GetString();
    }
}
