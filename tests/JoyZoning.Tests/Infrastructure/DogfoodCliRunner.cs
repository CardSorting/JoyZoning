using System.Diagnostics;
using System.Text;

namespace JoyZoning.Tests.Infrastructure;

/// <summary>Invokes jz CLI against a live control plane base URL.</summary>
internal static class DogfoodCliRunner
{
    private static string CliProjectPath()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 12; i++)
        {
            var candidate = Path.Combine(dir, "src", "JoyZoning.Cli", "JoyZoning.Cli.csproj");
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
            var parent = Directory.GetParent(dir);
            if (parent is null)
                break;
            dir = parent.FullName;
        }

        throw new FileNotFoundException("Could not locate src/JoyZoning.Cli/JoyZoning.Cli.csproj");
    }

    public static Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        string baseUrl,
        params string[] args) =>
        RunInWorktreeAsync(baseUrl, workingDirectory: null, args);

    public static async Task<(int ExitCode, string Stdout, string Stderr)> RunInWorktreeAsync(
        string baseUrl,
        string? workingDirectory,
        params string[] args)
    {
        var allArgs = new StringBuilder();
        allArgs.Append("run --project \"").Append(CliProjectPath()).Append("\" -- ");
        allArgs.Append("\"--base-url\" \"").Append(baseUrl.TrimEnd('/')).Append("\" ");
        foreach (var a in args)
        {
            allArgs.Append('"').Append(a.Replace("\"", "\\\"")).Append("\" ");
        }

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = allArgs.ToString().Trim(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (!string.IsNullOrWhiteSpace(workingDirectory))
            psi.WorkingDirectory = workingDirectory;
        psi.Environment["JOYZONING_URL"] = baseUrl.TrimEnd('/');

        using var process = Process.Start(psi)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, stdout, stderr);
    }
}
