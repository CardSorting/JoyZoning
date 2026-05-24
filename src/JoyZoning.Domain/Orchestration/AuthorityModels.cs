using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

public enum AuthorityRiskLevel
{
    Low,
    Medium,
    High,
    Critical,
}

public static class AuthorityReasonCodes
{
    public const string ProfileConservative = "profile_conservative";
    public const string AutopilotDisabled = "autopilot_disabled";
    public const string VerificationMissing = "verification_missing";
    public const string VerificationFailed = "verification_failed";
    public const string NotReadyForReview = "not_ready_for_review";
    public const string MergeConflict = "merge_conflict";
    public const string MergeFailed = "merge_failed";
    public const string OverlappingReadyWorker = "overlapping_ready_worker";
    public const string MergeObservabilityUnknown = "merge_observability_unknown";
    public const string CriticalRisk = "critical_risk";
    public const string HighRiskBalanced = "high_risk_balanced";
    public const string LargeChangeSet = "large_change_set";
    public const string UnknownHeadCommit = "unknown_head_commit";
    public const string DirtyWorktree = "dirty_worktree";
    public const string MetadataOnlyMode = "metadata_only_mode";
    public const string NoChangedFiles = "no_changed_files";
    public const string ProtectedPath = "protected_path";
    public const string CustomDeny = "custom_deny";
    public const string CustomAllowRequired = "custom_allow_required";
    public const string AutoAcceptEligible = "auto_accept_eligible";
    public const string JsdpHumanMergeRequired = "jsdp_human_merge_required";
}

public sealed record AuthorityEvaluationInput(
    AuthorityProfileKind Profile,
    bool AutopilotEnabled,
    bool MetadataOnlyAcceptResult,
    ExecutionLeaseStatus LeaseStatus,
    RiskLevel TaskRisk,
    string MergeState,
    bool VerificationPassed,
    bool VerificationMissing,
    bool HasConflicts,
    bool OverlappingReadyWorker,
    bool MergeObservabilityUnknown,
    bool DirtyWorktree,
    bool UnknownHeadCommit,
    int ChangedFilesCount,
    IReadOnlyList<string> ChangedFiles,
    int LargeChangeSetThreshold,
    CustomAuthorityRules? CustomRules = null,
    bool IsJsdpEnforcedSession = false);

public sealed record AuthorityDecision(
    AuthorityProfileKind Profile,
    AuthorityRiskLevel RiskLevel,
    bool AutoAcceptAllowed,
    bool NeedsHumanReview,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string> HumanMessages,
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<string> ProtectedPaths);

public sealed record AuthorityAutopilotDecision(
    AuthorityProfileKind Profile,
    AuthorityRiskLevel RiskLevel,
    bool AutoAcceptAllowed,
    bool NeedsHumanReview,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string> HumanMessages,
    DateTimeOffset? AutoAcceptedAt,
    bool WasAutoAccepted);

public static class AuthorityEvidence
{
    public const string DecisionKind = "authority.decision";
    public const string AutoAcceptedKind = "authority.auto_accepted";
    public const string AutoBlockedKind = "authority.auto_blocked";

    public static object ToEvidenceDetail(
        AuthorityDecision decision,
        bool attemptedAutoAccept,
        string mergeState,
        IReadOnlyList<string> overlappingPaths) =>
        new
        {
            fingerprint = Fingerprint(decision, mergeState, overlappingPaths),
            profile = decision.Profile.ToString(),
            riskLevel = decision.RiskLevel.ToString(),
            mergeState,
            overlappingPaths,
            decision.AutoAcceptAllowed,
            decision.NeedsHumanReview,
            reasonCodes = decision.ReasonCodes,
            humanMessages = decision.HumanMessages,
            changedFiles = decision.ChangedFiles,
            protectedPaths = decision.ProtectedPaths,
            attemptedAutoAccept,
        };

    public static object ToBlockedDetail(
        AuthorityDecision decision,
        string mergeState,
        IReadOnlyList<string> overlappingPaths,
        bool mergeObservabilityUnknown) =>
        new
        {
            fingerprint = BlockedFingerprint(decision, mergeState, overlappingPaths),
            profile = decision.Profile.ToString(),
            riskLevel = decision.RiskLevel.ToString(),
            mergeState,
            mergeObservabilityUnknown,
            overlappingPaths,
            reasonCodes = decision.ReasonCodes,
            humanMessages = decision.HumanMessages,
            changedFiles = decision.ChangedFiles,
            protectedPaths = decision.ProtectedPaths,
        };

    public static AuthorityAutopilotDecision? TryReadLast(string? evidenceLogJson)
    {
        var entries = LeaseEvidenceLog.DeserializeEntries(evidenceLogJson);
        AuthorityAutopilotDecision? fromAuto = null;
        AuthorityAutopilotDecision? fromBlocked = null;
        AuthorityAutopilotDecision? fromDecision = null;

        for (var i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            if (entry.Kind == AutoAcceptedKind)
            {
                fromAuto = ParseEntry(entry, wasAutoAccepted: true);
                break;
            }

            if (entry.Kind == AutoBlockedKind && fromBlocked is null)
                fromBlocked = ParseEntry(entry, wasAutoAccepted: false, forcedNeedsReview: true);

            if (entry.Kind == DecisionKind && fromDecision is null)
                fromDecision = ParseEntry(entry, wasAutoAccepted: false);
        }

        return fromAuto ?? fromBlocked ?? fromDecision;
    }

    public static bool ShouldAppendDecision(
        string? evidenceLogJson,
        AuthorityDecision decision,
        string mergeState,
        IReadOnlyList<string> overlappingPaths)
    {
        var fingerprint = Fingerprint(decision, mergeState, overlappingPaths);
        var last = FindLastAuthorityFingerprint(evidenceLogJson);
        return !string.Equals(last, fingerprint, StringComparison.Ordinal);
    }

    public static bool ShouldAppendBlocked(
        string? evidenceLogJson,
        AuthorityDecision decision,
        string mergeState,
        IReadOnlyList<string> overlappingPaths)
    {
        if (decision.AutoAcceptAllowed)
            return false;

        var blockedFingerprint = BlockedFingerprint(decision, mergeState, overlappingPaths);
        return !string.Equals(
            FindLastFingerprintForKind(evidenceLogJson, AutoBlockedKind),
            blockedFingerprint,
            StringComparison.Ordinal);
    }

    public static string BlockedFingerprint(
        AuthorityDecision decision,
        string mergeState,
        IReadOnlyList<string> overlappingPaths) =>
        "blocked:" + Fingerprint(decision, mergeState, overlappingPaths);

    public static string Fingerprint(
        AuthorityDecision decision,
        string mergeState,
        IReadOnlyList<string> overlappingPaths) =>
        string.Join(
            "\u001f",
            mergeState,
            decision.AutoAcceptAllowed.ToString(),
            string.Join(",", decision.ReasonCodes.Order(StringComparer.Ordinal)),
            string.Join(",", overlappingPaths.Order(StringComparer.OrdinalIgnoreCase)));

    private static string? FindLastAuthorityFingerprint(string? evidenceLogJson) =>
        FindLastFingerprintForKind(evidenceLogJson, DecisionKind);

    private static string? FindLastFingerprintForKind(string? evidenceLogJson, string kind)
    {
        var entries = LeaseEvidenceLog.DeserializeEntries(evidenceLogJson);
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            if (entry.Kind != kind)
                continue;

            if (entry.Detail is null)
                continue;

            try
            {
                var json = entry.Detail is System.Text.Json.JsonElement el
                    ? el.GetRawText()
                    : System.Text.Json.JsonSerializer.Serialize(entry.Detail);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("fingerprint", out var fp))
                    return fp.GetString();
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static AuthorityAutopilotDecision? ParseEntry(
        LeaseEvidenceEntry entry,
        bool wasAutoAccepted,
        bool forcedNeedsReview = false)
    {
        if (entry.Detail is null)
            return null;

        try
        {
            var json = entry.Detail is System.Text.Json.JsonElement el
                ? el.GetRawText()
                : System.Text.Json.JsonSerializer.Serialize(entry.Detail);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            var profile = Enum.TryParse<AuthorityProfileKind>(
                root.TryGetProperty("profile", out var p) ? p.GetString() : null,
                ignoreCase: true,
                out var prof)
                ? prof
                : AuthorityProfileKind.BalancedAuto;
            var risk = Enum.TryParse<AuthorityRiskLevel>(
                root.TryGetProperty("riskLevel", out var r) ? r.GetString() : null,
                ignoreCase: true,
                out var rl)
                ? rl
                : AuthorityRiskLevel.Medium;
            var codes = ReadStringArray(root, "reasonCodes");
            var messages = ReadStringArray(root, "humanMessages");

            var autoAccept = !forcedNeedsReview
                && root.TryGetProperty("autoAcceptAllowed", out var aa)
                && aa.GetBoolean();
            var needsReview = forcedNeedsReview
                || (root.TryGetProperty("needsHumanReview", out var nh) && nh.GetBoolean())
                || !autoAccept;

            return new AuthorityAutopilotDecision(
                profile,
                risk,
                autoAccept,
                needsReview,
                codes,
                messages,
                wasAutoAccepted ? entry.At : null,
                wasAutoAccepted);
        }
        catch
        {
            return null;
        }
    }

    private static List<string> ReadStringArray(System.Text.Json.JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var arr) || arr.ValueKind != System.Text.Json.JsonValueKind.Array)
            return [];

        return arr.EnumerateArray()
            .Select(e => e.GetString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .ToList();
    }
}
