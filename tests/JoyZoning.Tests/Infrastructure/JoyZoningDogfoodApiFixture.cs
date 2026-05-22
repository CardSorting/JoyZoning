using JoyZoning.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace JoyZoning.Tests.Infrastructure;

public sealed class JoyZoningDogfoodApiFixture : IDisposable
{
    public JoyZoningApiFactory Factory { get; } = new();
    private JoyZoningDogfoodServerProcess? _server;

    public HttpClient Client => Factory.CreateClient();

    public string PublicBaseUrl
    {
        get
        {
            _server ??= new JoyZoningDogfoodServerProcess(Factory.DatabasePath);
            return _server.BaseUrl;
        }
    }

    public async Task ResetAsync()
    {
        _server?.Dispose();
        _server = null;
        Factory.DietCode.FailNextDispatch = false;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JoyZoningDbContext>();
        await db.Database.EnsureDeletedAsync();
        scope.ServiceProvider.EnsureDatabaseCreated();
    }

    public void Dispose()
    {
        _server?.Dispose();
        Factory.Dispose();
    }
}
