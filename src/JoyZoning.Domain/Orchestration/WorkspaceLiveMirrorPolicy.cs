using JoyZoning.Domain.Configuration;

namespace JoyZoning.Domain.Orchestration;

public static class WorkspaceLiveMirrorPolicy
{
    public static bool UseSharedSessionRootMirror(
        bool mirrorEnabled,
        LiveMirrorMode mode,
        bool disableSharedWhenParallel,
        int activeLeasesInWorkspace)
    {
        if (!mirrorEnabled)
            return false;

        if (mode != LiveMirrorMode.SharedSessionRoot)
            return false;

        if (disableSharedWhenParallel && activeLeasesInWorkspace > 1)
            return false;

        return true;
    }

    public static bool UseIsolatedMirror(
        bool mirrorEnabled,
        LiveMirrorMode mode,
        bool disableSharedWhenParallel,
        int activeLeasesInWorkspace) =>
        mirrorEnabled
        && (mode is LiveMirrorMode.PerTask or LiveMirrorMode.PerExecution
            || (mode == LiveMirrorMode.SharedSessionRoot
                && disableSharedWhenParallel
                && activeLeasesInWorkspace > 1));
}
