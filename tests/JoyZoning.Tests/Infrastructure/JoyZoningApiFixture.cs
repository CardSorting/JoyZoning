using JoyZoning.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace JoyZoning.Tests.Infrastructure;

/// <summary>Shared API host for the OrchestrationApi xUnit collection (one factory, many test classes).</summary>
public sealed class JoyZoningApiCollectionFixture : IDisposable
{
    public JoyZoningApiFactory Factory { get; } = new();

    public HttpClient Client => Factory.CreateClient();

    public async Task ResetAsync()
    {
        Factory.DietCode.FailNextDispatch = false;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JoyZoningDbContext>();
        await TestDatabaseReset.ClearAllAsync(db);
    }

    public void Dispose() => Factory.Dispose();
}
