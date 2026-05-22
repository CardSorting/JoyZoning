using System.Diagnostics;

namespace JoyZoning.Agents.Hermes;

internal static class HermesCliLocator
{
    public static string? FindHermesExecutable(string installRoot)
    {
        foreach (var path in new[] { "hermes", "/usr/local/bin/hermes" })
        {
            try
            {
                var psi = new ProcessStartInfo(path, "--version")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                if (p is not null)
                {
                    p.WaitForExit(2000);
                    if (p.ExitCode == 0) return path;
                }
            }
            catch
            {
                // try next
            }
        }

        foreach (var venv in new[] { ".venv", "venv" })
        {
            var venvHermes = Path.Combine(installRoot, venv, "bin", "hermes");
            if (File.Exists(venvHermes))
                return venvHermes;
        }

        var localBin = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local",
            "bin",
            "hermes");
        if (File.Exists(localBin))
            return localBin;

        return null;
    }
}
