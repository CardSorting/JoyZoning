using System.Text.Json;

namespace JoyZoning.Domain.Orchestration;

/// <summary>Reads git convergence payloads from lease evidence entries.</summary>
public static class GitConvergenceEvidence
{
    public const string SucceededKind = "git.convergence.succeeded";
    public const string FailedKind = "git.convergence.failed";

    public static GitConvergenceSummary? TryReadLastSummary(string? evidenceLogJson)
    {
        var entries = LeaseEvidenceLog.DeserializeEntries(evidenceLogJson);
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            if (entry.Kind is not SucceededKind and not FailedKind)
                continue;

            if (entry.Detail is null)
                continue;

            try
            {
                var json = entry.Detail is JsonElement el ? el.GetRawText() : JsonSerializer.Serialize(entry.Detail);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var succeeded = entry.Kind == SucceededKind;
                var strategy = root.TryGetProperty("strategy", out var s) ? s.GetString() : null;
                var prev = root.TryGetProperty("destinationPreviousHead", out var p) ? p.GetString() : null;
                var next = root.TryGetProperty("destinationNewHead", out var n) ? n.GetString() : null;
                var err = root.TryGetProperty("errorMessage", out var e) ? e.GetString() : null;
                var files = root.TryGetProperty("changedFiles", out var cf)
                    ? cf.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList()
                    : new List<string>();

                return new GitConvergenceSummary(
                    succeeded,
                    strategy ?? "unknown",
                    prev,
                    next,
                    files,
                    err);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    public static object ToEvidenceDetail(WorkspaceConvergenceResult result) =>
        new
        {
            result.Strategy,
            result.SourceWorktreePath,
            result.SourceBranch,
            result.SourceHeadCommit,
            result.DestinationWorkspaceRoot,
            result.DestinationBranch,
            destinationPreviousHead = result.DestinationPreviousHead,
            destinationNewHead = result.DestinationNewHead,
            result.ChangedFiles,
            result.ConflictFiles,
            result.ErrorMessage,
            result.CompletedAt,
        };
}
