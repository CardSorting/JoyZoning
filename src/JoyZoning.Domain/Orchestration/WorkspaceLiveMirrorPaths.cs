using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Entities;

namespace JoyZoning.Domain.Orchestration;

public static class WorkspaceLiveMirrorPaths
{
    public const string LiveRootSegment = ".joyzoning/live";

    public static bool TryResolveMirrorRoot(
        string sessionWorkspaceRoot,
        Guid taskId,
        Guid mirrorKeyId,
        LiveMirrorMode mode,
        out string mirrorRoot,
        out string? error)
    {
        mirrorRoot = string.Empty;
        error = null;

        if (taskId == Guid.Empty || mirrorKeyId == Guid.Empty)
        {
            error = "Task and mirror key ids must be non-empty GUIDs.";
            return false;
        }

        if (!WorkspacePaths.TryNormalize(sessionWorkspaceRoot, out var sessionRoot))
        {
            error = "Invalid session workspace root.";
            return false;
        }

        var liveBase = Path.GetFullPath(Path.Combine(sessionRoot, LiveRootSegment));
        if (!EnsureUnderRoot(sessionRoot, liveBase))
        {
            error = "Live mirror base escapes session workspace.";
            return false;
        }

        Directory.CreateDirectory(liveBase);

        var taskSegment = taskId.ToString("D");
        if (!IsSafeGuidSegment(taskSegment))
        {
            error = "Invalid task id segment.";
            return false;
        }

        var taskDir = Path.GetFullPath(Path.Combine(liveBase, taskSegment));
        if (!EnsureUnderRoot(liveBase, taskDir))
        {
            error = "Task mirror path escapes live root.";
            return false;
        }

        if (mode == LiveMirrorMode.PerTask)
        {
            mirrorRoot = taskDir;
            return EnsureUnderRoot(sessionRoot, mirrorRoot);
        }

        var keySegment = mirrorKeyId.ToString("D");
        if (!IsSafeGuidSegment(keySegment))
        {
            error = "Invalid mirror key segment.";
            return false;
        }

        mirrorRoot = Path.GetFullPath(Path.Combine(taskDir, keySegment));
        if (!EnsureUnderRoot(taskDir, mirrorRoot))
        {
            error = "Execution mirror path escapes task folder.";
            return false;
        }

        return EnsureUnderRoot(sessionRoot, mirrorRoot);
    }

    public static Guid ResolveMirrorKeyId(ExecutionLease lease, LiveMirrorMode mode)
    {
        if (mode == LiveMirrorMode.PerTask)
            return lease.WorkTaskId;

        return lease.ExecutionSessionId is { } executionId && executionId != Guid.Empty
            ? executionId
            : lease.Id;
    }

    public static bool IsSafeGuidSegment(string segment) =>
        Guid.TryParse(segment, out _);

    private static bool EnsureUnderRoot(string root, string candidate)
    {
        if (!WorkspacePaths.TryNormalize(root, out var normRoot)
            || !WorkspacePaths.TryNormalize(candidate, out var normCandidate))
            return false;

        return normCandidate.Equals(normRoot, StringComparison.OrdinalIgnoreCase)
            || normCandidate.StartsWith(
                normRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }
}
