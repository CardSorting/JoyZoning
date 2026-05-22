using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JoyZoning.Tests.Infrastructure;

/// <summary>One factory instance per test class instance — isolated SQLite file per test method.</summary>
public sealed class JoyZoningApiFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly string _dbPath;
    private readonly TestDietCodeAdapter _dietCode = new();

    public TestDietCodeAdapter DietCode => _dietCode;

    internal string DatabasePath => _dbPath;

    public JoyZoningApiFactory()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "jz-api-test-" + Guid.NewGuid().ToString("N") + ".db");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        JoyZoningTestHostConfigurer.Configure(builder, _dbPath, _dietCode);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { /* best effort */ }
        }
    }
}
