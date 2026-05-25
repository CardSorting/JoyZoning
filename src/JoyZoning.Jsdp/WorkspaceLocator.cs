namespace JoyZoning.Jsdp;

public static class WorkspaceLocator
{
    /// <summary>Resolve workspace for an existing run (walks up for <c>.jsdp/run.json</c>).</summary>
    public static string Find(string? start = null)
    {
        var dir = new DirectoryInfo(start ?? Environment.CurrentDirectory);
        for (var current = dir; current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, JsdpPaths.RootDir, JsdpPaths.RunFile)))
                return current.FullName;
        }

        return dir.FullName;
    }

    /// <summary>Workspace root for <c>jsdp init</c> — current directory, not repo parent.</summary>
    public static string ForInit(string? start = null) =>
        new DirectoryInfo(start ?? Environment.CurrentDirectory).FullName;
}
