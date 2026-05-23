using System.Diagnostics;

namespace JoyZoning.Cli;

/// <summary>Runs the workspace-live.sh progress tracker (friendly terminal UI).</summary>
public static class TaskWatchCommand
{
    public static int Run(CliContext ctx, Guid taskId)
    {
        var script = FindWorkspaceLiveScript();
        if (script is null)
        {
            CliOutput.WriteUsageError(
                "workspace-live.sh not found. Set JOYZONING_ROOT to your JoyZoning checkout, " +
                "or run from the repo: ./scripts/workspace-live.sh <task-id> [workspace]");
            return 2;
        }

        var workspace = CliArgs.OptStatic(ctx.Args.Raw, "--workspace")
            ?? Environment.GetEnvironmentVariable("JOYZONING_WORKSPACE")
            ?? Directory.GetCurrentDirectory();

        var psi = new ProcessStartInfo
        {
            FileName = script,
            ArgumentList = { taskId.ToString(), workspace },
            UseShellExecute = false,
        };

        if (ctx.Args.Has("--once"))
            psi.ArgumentList.Add("--once");
        if (ctx.Args.Has("--simple"))
            psi.ArgumentList.Add("--simple");
        if (ctx.Args.Has("--paths"))
            psi.ArgumentList.Add("--paths");
        if (ctx.Args.Has("--notify"))
            psi.Environment["JOYZONING_LIVE_NOTIFY"] = "1";
        if (ctx.Args.OptStatic(ctx.Args.Raw, "--interval") is { } interval)
            psi.Environment["JOYZONING_LIVE_INTERVAL"] = interval;

        var url = ctx.Args.OptStatic(ctx.Args.Raw, "--base-url")
            ?? Environment.GetEnvironmentVariable("JOYZONING_URL")
            ?? "http://127.0.0.1:9470";
        psi.Environment["JOYZONING_URL"] = url;

        using var proc = Process.Start(psi);
        return proc is null ? 1 : proc.WaitForExit();
    }

    internal static string? FindWorkspaceLiveScript()
    {
        var candidates = new List<string>();
        var root = Environment.GetEnvironmentVariable("JOYZONING_ROOT");
        if (!string.IsNullOrWhiteSpace(root))
            candidates.Add(Path.Combine(root, "scripts", "workspace-live.sh"));

        var dir = Directory.GetCurrentDirectory();
        for (var i = 0; i < 6 && !string.IsNullOrEmpty(dir); i++)
        {
            candidates.Add(Path.Combine(dir, "scripts", "workspace-live.sh"));
            var parent = Directory.GetParent(dir);
            dir = parent?.FullName ?? "";
        }

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }
}
