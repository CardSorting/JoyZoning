using JoyZoning.Domain.Orchestration;

namespace JoyZoning.Cli;

/// <summary>Guards against YOLO invoking human-only or escape-hatch APIs.</summary>
public static class YoloForbiddenCalls
{
    public static readonly HashSet<string> ForbiddenEvidenceKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "lease.merged",
        "task.complete",
    };

    public static void RejectIfForbidden(string operation)
    {
        var forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "MergeLease",
            "merge",
            "Complete",
            "complete",
            "RevokeLease",
            "revoke",
            "RawAsync",
            "raw",
            "UpdateTaskStatus.Complete",
        };

        if (forbidden.Contains(operation))
            throw new YoloPolicyViolationException(
                $"YOLO mode cannot invoke {operation}. Human operators own merge, Complete, and revoke.");
    }

    public static void RejectRevokeUnlessAllowed(bool allowRevoke)
    {
        if (!allowRevoke)
            RejectIfForbidden("revoke");
    }
}
