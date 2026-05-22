using System.Diagnostics;

namespace JoyZoning.App.Services;

/// <summary>Starts the local control plane if it is not already listening on :9470.</summary>
public static class ControlPlaneHost
{
    private static Process? _process;

    public static async Task<bool> EnsureRunningAsync(CancellationToken cancellationToken = default)
    {
        var client = new ControlPlaneClient();
        if (await client.IsHealthyAsync())
            return true;

        var project = FindControlPlaneProject();
        if (project is null)
            return false;

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{project}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        _process = Process.Start(psi);

        for (var i = 0; i < 40; i++)
        {
            await Task.Delay(500, cancellationToken);
            if (await client.IsHealthyAsync())
                return true;
        }

        return false;
    }

    private static string? FindControlPlaneProject()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(dir, "src", "JoyZoning.ControlPlane", "JoyZoning.ControlPlane.csproj");
            if (File.Exists(candidate))
                return candidate;

            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }

        var env = Environment.GetEnvironmentVariable("JOYZONING_CONTROL_PLANE_PROJECT");
        return !string.IsNullOrEmpty(env) && File.Exists(env) ? env : null;
    }
}
