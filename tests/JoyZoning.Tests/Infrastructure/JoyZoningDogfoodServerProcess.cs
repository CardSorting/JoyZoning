using System.Diagnostics;
using System.Net;

namespace JoyZoning.Tests.Infrastructure;

/// <summary>Runs ControlPlane on a real port (shared SQLite with the test factory).</summary>
internal sealed class JoyZoningDogfoodServerProcess : IDisposable
{
    private readonly Process _process;

    public string BaseUrl { get; }

    public JoyZoningDogfoodServerProcess(string databasePath)
    {
        var port = FindFreePort();
        BaseUrl = $"http://127.0.0.1:{port}";

        var project = ControlPlaneProjectPath();
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{project}\" --no-launch-profile",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.Environment["ASPNETCORE_URLS"] = BaseUrl;
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
        psi.Environment["JoyZoning__DatabasePath"] = databasePath;

        _process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start control plane for dogfood.");

        WaitForHealthy();
    }

    private void WaitForHealthy()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow.AddSeconds(30);
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var resp = http.GetAsync($"{BaseUrl}/api/health").GetAwaiter().GetResult();
                if (resp.IsSuccessStatusCode)
                    return;
            }
            catch (Exception ex)
            {
                last = ex;
            }

            if (_process.HasExited)
                throw new InvalidOperationException(
                    $"Control plane exited during startup (code {_process.ExitCode}). stderr: {_process.StandardError.ReadToEnd()}");

            Thread.Sleep(200);
        }

        throw new TimeoutException($"Control plane did not become healthy at {BaseUrl}.", last);
    }

    private static int FindFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string ControlPlaneProjectPath()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 12; i++)
        {
            var candidate = Path.Combine(dir, "src", "JoyZoning.ControlPlane", "JoyZoning.ControlPlane.csproj");
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
            var parent = Directory.GetParent(dir);
            if (parent is null)
                break;
            dir = parent.FullName;
        }

        throw new FileNotFoundException("Could not locate JoyZoning.ControlPlane.csproj");
    }

    public void Dispose()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(5000);
            }
        }
        catch { /* best effort */ }
        finally
        {
            _process.Dispose();
        }
    }
}
