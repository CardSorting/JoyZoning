using JoyZoning.Domain.Entities;
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
        return sessions
            .GroupBy(WorkspaceKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(s => s.UpdatedAt).First())
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
            .Where(s => WorkspacePaths.EqualsNormalized(s.WorkspaceRoot, norm))
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefault();
    }
}
