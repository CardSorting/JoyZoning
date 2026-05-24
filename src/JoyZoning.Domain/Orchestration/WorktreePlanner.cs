using System.Security.Cryptography;
using System.Text;
using JoyZoning.Domain.Entities;

namespace JoyZoning.Domain.Orchestration;

/// <summary>Plans execution workspace metadata — JSDP uses the canonical session root only.</summary>
public static class WorktreePlanner
{
    private static readonly HashSet<char> SafeHex = "0123456789abcdef".ToHashSet();

    public static bool TryPlan(
        string workspaceRoot,
        Guid cardId,
        out string worktreePath,
        out string branchName,
        out string? error) =>
        TryPlan(workspaceRoot, cardId, hermesKanbanTaskId: null, boundedSession: null, out worktreePath, out branchName, out error);

    public static bool TryPlan(
        string workspaceRoot,
        Guid cardId,
        string? hermesKanbanTaskId,
        out string worktreePath,
        out string branchName,
        out string? error) =>
        TryPlan(workspaceRoot, cardId, hermesKanbanTaskId, boundedSession: null, out worktreePath, out branchName, out error);

    public static bool TryPlan(
        string workspaceRoot,
        Guid cardId,
        string? hermesKanbanTaskId,
        OperatorSession? boundedSession,
        out string worktreePath,
        out string branchName,
        out string? error)
    {
        worktreePath = string.Empty;
        branchName = string.Empty;
        error = null;

        if (cardId == Guid.Empty)
        {
            error = "Invalid card id.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            error = "Workspace root is required.";
            return false;
        }

        string workspaceFull;
        try
        {
            workspaceFull = Path.GetFullPath(workspaceRoot.Trim());
        }
        catch
        {
            error = "Workspace root is not a valid path.";
            return false;
        }

        if (!Directory.Exists(workspaceFull))
        {
            error = "Workspace root does not exist.";
            return false;
        }

        var shortId = ResolveWorktreeSegment(cardId, hermesKanbanTaskId);
        if (shortId is null)
        {
            error = "Card id cannot be used for branch naming.";
            return false;
        }

        branchName = $"joyzoning/card-{shortId}";
        worktreePath = WorkspacePaths.NormalizeComparable(workspaceFull);
        return true;
    }

    public static (string WorktreePath, string BranchName) Plan(string workspaceRoot, Guid cardId)
    {
        if (!TryPlan(workspaceRoot, cardId, out var worktreePath, out var branchName, out var error))
            throw LeaseOrchestrationException.BadRequest(error!);

        return (worktreePath, branchName);
    }

    private static string? ResolveWorktreeSegment(Guid cardId, string? hermesKanbanTaskId)
    {
        if (!string.IsNullOrWhiteSpace(hermesKanbanTaskId))
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(hermesKanbanTaskId.Trim()));
            return Convert.ToHexString(hash)[..8].ToLowerInvariant();
        }

        return SanitizeCardIdSegment(cardId);
    }

    private static string? SanitizeCardIdSegment(Guid cardId)
    {
        var hex = cardId.ToString("N");
        if (hex.Length < 8 || hex.Any(c => !SafeHex.Contains(char.ToLowerInvariant(c))))
            return null;

        return hex[..8].ToLowerInvariant();
    }
}
