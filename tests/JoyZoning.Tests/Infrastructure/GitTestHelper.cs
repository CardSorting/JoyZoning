using System.Diagnostics;

namespace JoyZoning.Tests.Infrastructure;

internal static class GitTestHelper
{
    public static void InitRepo(string root)
    {
        Run("init", root);
        Run("config user.email test@joyzoning.local", root);
        Run("config user.name JoyZoning Test", root);
        File.WriteAllText(Path.Combine(root, ".gitkeep"), "");
        Run("add .", root);
        Run("commit -m init", root);
    }

    private static void Run(string args, string cwd)
    {
        var psi = new ProcessStartInfo("git", args)
        {
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("git not available");
        p.WaitForExit(10_000);
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"git {args} failed: {p.StandardError.ReadToEnd()}");
    }
}
