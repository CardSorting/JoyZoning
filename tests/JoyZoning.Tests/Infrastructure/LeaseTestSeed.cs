using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using JoyZoning.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace JoyZoning.Tests.Infrastructure;

/// <summary>Seeds lease DB state for API tests that need a pre-existing verification report.</summary>
internal static class LeaseTestSeed
{
    public static async Task SetVerifyingWithPassingReportAsync(
        IServiceProvider services,
        Guid taskId,
        VerificationReport report)
    {
        using var scope = services.CreateScope();
        var leases = scope.ServiceProvider.GetRequiredService<IExecutionLeaseRepository>();
        var lease = await leases.GetActiveByTaskIdAsync(taskId)
            ?? throw new InvalidOperationException("No active lease to seed.");

        lease.Status = ExecutionLeaseStatus.Verifying;
        lease.VerificationReportJson = VerificationReportSerializer.Serialize(report);
        await leases.UpdateAsync(lease);
    }

    public static async Task StaleHeartbeatAsync(IServiceProvider services, Guid taskId)
    {
        using var scope = services.CreateScope();
        var leases = scope.ServiceProvider.GetRequiredService<IExecutionLeaseRepository>();
        var lease = await leases.GetActiveByTaskIdAsync(taskId)
            ?? throw new InvalidOperationException("No active lease.");
        lease.LastHeartbeatAt = DateTimeOffset.UtcNow.AddHours(-2);
        await leases.UpdateAsync(lease);
    }
}
