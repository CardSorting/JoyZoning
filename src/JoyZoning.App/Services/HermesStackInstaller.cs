using System.Diagnostics;
using System.Text;

namespace JoyZoning.App.Services;

/// <summary>
/// Ensures the single diet-hermes checkout is built and configured for JoyZoning.
/// </summary>
public static class HermesStackInstaller
{
    public record InstallResult(bool Success, string? DietHermesRoot, string Message);

    public static async Task<InstallResult> EnsureInstalledAsync(
        Action<string> reportStatus,
        Action<int> reportProgress,
        CancellationToken cancellationToken = default)
    {
        var canonical = HermesInstallPaths.CanonicalInstallRoot;
        var existing = OnboardingPathDiscovery.BestGuess(canonical);
        if (!string.IsNullOrWhiteSpace(existing)
            && OnboardingEvaluator.ValidateInstallRoot(existing).IsValid)
        {
            return new InstallResult(true, existing, "diet-hermes is ready.");
        }

        var script = LocateInstallScript();
        if (script is null)
        {
            return new InstallResult(false, null,
                $"Run ./scripts/install-diet-hermes.sh in JoyZoning, or ./setup-hermes.sh in {canonical}.");
        }

        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
        {
            return new InstallResult(false, null,
                "Automatic diet-hermes setup is supported on macOS and Linux only.");
        }

        try
        {
            chmodExecutable(script);
        }
        catch
        {
            // non-fatal
        }

        var prefs = OnboardingPreferences.Load();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";

        var psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(script);

        psi.EnvironmentVariables["HOME"] = home;
        psi.EnvironmentVariables["JOYZONING_DIET_HERMES_DIR"] =
            string.IsNullOrWhiteSpace(prefs.DietHermesInstallPath)
                ? canonical
                : prefs.DietHermesInstallPath!;
        psi.EnvironmentVariables["JOYZONING_HERMES_PROFILE"] = "joyzoning";
        if (!string.IsNullOrWhiteSpace(prefs.DietHermesGitUrl))
            psi.EnvironmentVariables["DIET_HERMES_GIT_URL"] = prefs.DietHermesGitUrl;
        psi.EnvironmentVariables["PATH"] = $"{Path.Combine(home, ".local", "bin")}:{pathEnv}";

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stderr = new StringBuilder();

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                stderr.AppendLine(e.Data);
        };

        process.Start();
        process.BeginErrorReadLine();

        string? installRoot = null;
        await foreach (var line in ReadLinesAsync(process.StandardOutput, cancellationToken))
        {
            if (line.StartsWith("JZ_STATUS:", StringComparison.Ordinal))
            {
                reportStatus(line["JZ_STATUS:".Length..]);
                continue;
            }

            if (line.StartsWith("JZ_PROGRESS:", StringComparison.Ordinal)
                && int.TryParse(line["JZ_PROGRESS:".Length..], out var pct))
            {
                reportProgress(pct);
                continue;
            }

            if (line.StartsWith("JZ_INSTALL_ROOT:", StringComparison.Ordinal))
                installRoot = line["JZ_INSTALL_ROOT:".Length..].Trim();
        }

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var err = stderr.ToString().Trim();
            return new InstallResult(false, installRoot,
                string.IsNullOrWhiteSpace(err)
                    ? $"diet-hermes setup failed (exit {process.ExitCode})."
                    : $"diet-hermes setup failed: {err}");
        }

        installRoot ??= OnboardingPathDiscovery.BestGuess(canonical);
        if (string.IsNullOrWhiteSpace(installRoot)
            || !OnboardingEvaluator.ValidateInstallRoot(installRoot).IsValid)
        {
            return new InstallResult(false, installRoot,
                "Setup finished but hermes CLI was not found. Open Getting Started for details.");
        }

        return new InstallResult(true, installRoot,
            "diet-hermes is ready — Manager and executor share this install; kanban syncs work between sessions.");
    }

    private static async IAsyncEnumerable<string> ReadLinesAsync(
        StreamReader reader,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is not null)
                yield return line;
        }
    }

    private static string? LocateInstallScript()
    {
        var names = new[] { "install-diet-hermes.sh", "install-hermes-stack.sh" };
        var candidates = new List<string>();

        var asmDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (!string.IsNullOrEmpty(asmDir))
        {
            foreach (var name in names)
            {
                candidates.Add(Path.Combine(asmDir, "scripts", name));
                candidates.Add(Path.GetFullPath(Path.Combine(asmDir, "..", "..", "..", "..", "scripts", name)));
            }
        }

        var cwd = Directory.GetCurrentDirectory();
        foreach (var name in names)
        {
            candidates.Add(Path.Combine(cwd, "scripts", name));
            candidates.Add(Path.Combine(cwd, "..", "scripts", name));
        }

        var joyRepo = Environment.GetEnvironmentVariable("JOYZONING_REPO");
        if (!string.IsNullOrWhiteSpace(joyRepo))
        {
            foreach (var name in names)
                candidates.Add(Path.Combine(joyRepo, "scripts", name));
        }

        foreach (var path in candidates.Distinct(StringComparer.Ordinal))
        {
            try
            {
                var full = Path.GetFullPath(path);
                if (File.Exists(full))
                    return full;
            }
            catch
            {
                // ignore
            }
        }

        return null;
    }

    private static void chmodExecutable(string script)
    {
        var psi = new ProcessStartInfo("chmod", $"+x \"{script}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi);
        p?.WaitForExit(3000);
    }
}
