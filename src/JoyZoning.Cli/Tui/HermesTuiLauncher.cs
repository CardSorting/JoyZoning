using System.Diagnostics;
using JoyZoning.Agents.Hermes;

namespace JoyZoning.Cli.Tui;

/// <summary>Delegates interactive agent chat to diet-hermes --tui (do not reimplement Hermes Ink).</summary>
public static class HermesTuiLauncher
{
    public static async Task<int> LaunchAsync(
        string installRoot,
        string profile,
        bool resumeLatest,
        CancellationToken cancellationToken = default)
    {
        var hermes = HermesCliLocator.FindHermesExecutable(installRoot);
        if (hermes is null)
        {
            Console.Error.WriteLine(
                $"Hermes CLI not found under install root: {installRoot}. Run jz doctor or set install root in the desktop.");
            return 2;
        }

        var args = new List<string> { "--tui", "-p", profile };
        if (resumeLatest)
            args.Add("-c");

        Console.WriteLine($"  Launching {hermes} {string.Join(' ', args)} …");
        Console.WriteLine("  (JoyZoning operator TUI resumes when Hermes exits.)");
        Console.WriteLine();

        var psi = new ProcessStartInfo(hermes, args)
        {
            WorkingDirectory = installRoot,
            UseShellExecute = false,
            RedirectStandardInput = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
        };

        using var process = Process.Start(psi);
        if (process is null)
            return 2;

        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }

    public static async Task<(string InstallRoot, string Profile)> ResolveHermesConfigAsync(
        JoyZoningCliClient client,
        CancellationToken cancellationToken = default)
    {
        var config = await client.GetConfigAsync();
        var installRoot = "";
        var profile = "joyzoning";

        if (config.IsSuccess && config.Body.HasValue)
        {
            var body = config.Body.Value;
            if (body.TryGetProperty("installRoot", out var ir))
                installRoot = ir.GetString() ?? "";
            if (body.TryGetProperty("profile", out var pr))
                profile = pr.GetString() ?? profile;
        }

        if (string.IsNullOrWhiteSpace(installRoot))
            installRoot = Environment.GetEnvironmentVariable("HERMES_INSTALL_ROOT")
                ?? "/Users/bozoegg/Downloads/diet-hermes-main-master";

        return (installRoot, profile);
    }
}
