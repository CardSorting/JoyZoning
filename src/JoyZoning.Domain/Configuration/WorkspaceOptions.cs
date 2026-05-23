namespace JoyZoning.Domain.Configuration;

/// <summary>
/// Mirrors active lease worktrees into the operator session workspace root and
/// writes a human-readable progress file for IDE / terminal visibility.
/// </summary>
public sealed class WorkspaceOptions
{
    public const string SectionName = "Workspace";

    /// <summary>Copy DietCode worktree files into session.WorkspaceRoot (excludes node_modules, .git).</summary>
    public bool MirrorToSessionRoot { get; set; } = true;

    /// <summary>Filename written at the session workspace root.</summary>
    public string LiveStatusFileName { get; set; } = "JOYZONING_LIVE.md";

    /// <summary>Background worktree scan interval when <see cref="MirrorToSessionRoot"/> is enabled.</summary>
    public int LiveMonitorIntervalSeconds { get; set; } = 4;

    /// <summary>Write machine-readable progress to <c>.joyzoning/live.json</c> in the session workspace.</summary>
    public bool WriteLiveJsonFile { get; set; } = true;

    /// <summary>Relative path under session workspace for JSON progress (IDE / scripts).</summary>
    public string LiveJsonRelativePath { get; set; } = ".joyzoning/live.json";
}
