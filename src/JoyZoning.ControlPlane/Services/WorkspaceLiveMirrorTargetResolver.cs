using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Orchestration;

namespace JoyZoning.ControlPlane.Services;

internal static class WorkspaceLiveMirrorTargetResolver
{
    internal sealed record MirrorTarget(
        string MirrorRoot,
        string SessionRoot,
        bool IsSharedSessionRoot,
        Guid MirrorKeyId,
        LiveMirrorMode Mode);

    internal static bool TryResolve(
        ExecutionLease lease,
        string sessionRoot,
        bool mirrorEnabled,
        WorkspaceParallelismOptions parallelism,
        int activeLeasesInWorkspace,
        out MirrorTarget target,
        out string? error)
    {
        target = default!;
        error = null;

        var mode = parallelism.LiveMirrorMode;
        var useShared = WorkspaceLiveMirrorPolicy.UseSharedSessionRootMirror(
            mirrorEnabled,
            mode,
            parallelism.DisableSharedSessionRootMirrorWhenParallel,
            activeLeasesInWorkspace);

        if (useShared)
        {
            target = new MirrorTarget(
                sessionRoot,
                sessionRoot,
                IsSharedSessionRoot: true,
                MirrorKeyId: lease.WorkTaskId,
                Mode: LiveMirrorMode.SharedSessionRoot);
            return true;
        }

        if (!WorkspaceLiveMirrorPolicy.UseIsolatedMirror(
                mirrorEnabled,
                mode,
                parallelism.DisableSharedSessionRootMirrorWhenParallel,
                activeLeasesInWorkspace))
        {
            error = "Live mirroring is disabled for this configuration.";
            return false;
        }

        var effectiveMode = mode == LiveMirrorMode.SharedSessionRoot
            ? LiveMirrorMode.PerExecution
            : mode;

        var mirrorKeyId = WorkspaceLiveMirrorPaths.ResolveMirrorKeyId(lease, effectiveMode);
        if (!WorkspaceLiveMirrorPaths.TryResolveMirrorRoot(
                sessionRoot,
                lease.WorkTaskId,
                mirrorKeyId,
                effectiveMode,
                out var mirrorRoot,
                out error))
            return false;

        target = new MirrorTarget(
            mirrorRoot,
            sessionRoot,
            IsSharedSessionRoot: false,
            MirrorKeyId: mirrorKeyId,
            Mode: effectiveMode);
        return true;
    }
}
