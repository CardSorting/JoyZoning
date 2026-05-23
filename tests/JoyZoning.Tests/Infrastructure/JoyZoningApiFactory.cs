using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using JoyZoning.Persistence;

namespace JoyZoning.Tests.Infrastructure;

/// <summary>Shared in-memory SQLite for the OrchestrationApi collection (no disk I/O, safe with an open host).</summary>
public sealed class JoyZoningApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _sqlite;
    private readonly TestDietCodeAdapter _dietCode = new();

    public TestDietCodeAdapter DietCode => _dietCode;

    internal string DatabasePath => _sqlite.DataSource;

    public JoyZoningApiFactory()
    {
        var name = "jz-api-" + Guid.NewGuid().ToString("N");
        _sqlite = new SqliteConnection($"Data Source={name};Mode=Memory;Cache=Shared");
        _sqlite.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        JoyZoningTestHostConfigurer.Configure(builder, _sqlite, _dietCode);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _sqlite.Close(); } catch { /* ignore */ }
            try { _sqlite.Dispose(); } catch { /* ignore */ }
        }

        base.Dispose(disposing);
    }
}
