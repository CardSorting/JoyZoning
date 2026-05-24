using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;

namespace JoyZoning.ControlPlane.Services;

/// <summary>Groups operator sessions by physical workspace folder.</summary>
public static class WorkspaceSessionCatalog
{
    public static string WorkspaceKey(OperatorSession session) =>
        WorkspacePaths.TryNormalize(session.WorkspaceRoot, out var norm)
            ? norm
            : session.WorkspaceRoot.Trim();

    public static IReadOnlyList<OperatorSession> SelectCanonicalSessions(
        IEnumerable<OperatorSession> sessions)
    {
        var materialized = sessions.ToList();
        var boundedRole = materialized
            .Where(s => s.ExecutionMode == SessionExecutionMode.BoundedRole)
            .OrderByDescending(s => s.UpdatedAt)
            .ToList();

        var defaultCanonical = materialized
            .Where(s => s.ExecutionMode != SessionExecutionMode.BoundedRole)
            .GroupBy(WorkspaceKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(s => s.UpdatedAt).First())
            .ToList();

        return boundedRole
            .Concat(defaultCanonical)
            .OrderByDescending(s => s.UpdatedAt)
            .ToList();
    }

    public static OperatorSession? FindCanonicalForWorkspace(
        IEnumerable<OperatorSession> sessions,
        string workspaceRoot)
    {
        if (!WorkspacePaths.TryNormalize(workspaceRoot, out var norm))
            norm = workspaceRoot.Trim();

        return sessions
            .Where(s => s.ExecutionMode != SessionExecutionMode.BoundedRole)
            .Where(s => WorkspacePaths.EqualsNormalized(s.WorkspaceRoot, norm))
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefault();
    }
}
