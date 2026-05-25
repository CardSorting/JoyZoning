using System.Diagnostics;

namespace JoyZoning.Jsdp;

/// <summary>Cross-platform shell execution without fragile <c>bash -lc</c> quoting.</summary>
public static class ShellRunner
{
    public static (int ExitCode, string Stdout, string Stderr) Run(string workingDirectory, string command, int timeoutMs = 600_000)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            if (OperatingSystem.IsWindows())
            {
                psi.FileName = "cmd.exe";
                psi.ArgumentList.Add("/c");
                psi.ArgumentList.Add(command);
            }
            else
            {
                psi.FileName = "/bin/bash";
                psi.ArgumentList.Add("-c");
                psi.ArgumentList.Add(command);
            }

            using var process = Process.Start(psi);
            if (process is null)
                return (-1, "", "process did not start");

            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(); } catch { /* ignore */ }
                return (-1, "", "process timed out");
            }

            return (process.ExitCode, process.StandardOutput.ReadToEnd(), process.StandardError.ReadToEnd());
        }
        catch (Exception ex)
        {
            return (-1, "", ex.Message);
        }
    }
}
