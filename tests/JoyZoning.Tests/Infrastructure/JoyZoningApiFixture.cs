using JoyZoning.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JoyZoning.Tests.Infrastructure;

public sealed class JoyZoningApiFixture : IDisposable
{
    public JoyZoningApiFactory Factory { get; } = new();

    public HttpClient Client => Factory.CreateClient();

    public async Task ResetAsync()
    {
        Factory.DietCode.FailNextDispatch = false;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JoyZoningDbContext>();
        await db.Database.EnsureDeletedAsync();
        scope.ServiceProvider.EnsureDatabaseCreated();
    }

    public void Dispose() => Factory.Dispose();
}
