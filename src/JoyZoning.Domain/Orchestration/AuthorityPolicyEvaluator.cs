using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

/// <summary>Evaluates whether a ReadyForReview worker may be auto-accepted under the active profile.</summary>
public static class AuthorityPolicyEvaluator
{
    public static AuthorityDecision Evaluate(AuthorityEvaluationInput input)
    {
        var files = input.ChangedFiles.Count > 0
            ? input.ChangedFiles
            : [];
        var protectedHits = ProtectedPathMatcher.FindProtected(files);
        var risk = MapRisk(input.TaskRisk, input.ChangedFilesCount, protectedHits.Count > 0);
        var codes = new List<string>();
        var messages = new List<string>();

        void Block(string code, string message)
        {
            if (!codes.Contains(code))
                codes.Add(code);
            messages.Add(message);
        }

        if (!input.AutopilotEnabled)
            Block(AuthorityReasonCodes.AutopilotDisabled, "Autopilot is disabled for this deployment.");

        if (input.Profile == AuthorityProfileKind.Conservative)
            Block(AuthorityReasonCodes.ProfileConservative, "Conservative profile: human review required before accept.");

        if (input.MetadataOnlyAcceptResult)
            Block(AuthorityReasonCodes.MetadataOnlyMode, "Metadata-only accept mode: real git convergence is disabled.");

        if (input.LeaseStatus != ExecutionLeaseStatus.ReadyForReview)
            Block(AuthorityReasonCodes.NotReadyForReview, "Worker is not ReadyForReview.");

        if (input.VerificationMissing)
            Block(AuthorityReasonCodes.VerificationMissing, "Blocked: verification missing.");

        if (!input.VerificationPassed)
            Block(AuthorityReasonCodes.VerificationFailed, "Blocked: verification failed.");

        if (input.MergeState == WorkerMergeStateNames.ToApiString(WorkerMergeState.MergeConflict))
            Block(AuthorityReasonCodes.MergeConflict, "Blocked: merge conflict.");

        if (input.MergeState == WorkerMergeStateNames.ToApiString(WorkerMergeState.MergeFailed))
            Block(AuthorityReasonCodes.MergeFailed, "Blocked: merge preconditions failed.");

        if (input.HasConflicts)
            Block(AuthorityReasonCodes.MergeConflict, "Blocked: unresolved conflicts.");

        if (input.OverlappingReadyWorker)
        {
            var overlapHint = input.ChangedFiles.Count > 0
                ? string.Join(", ", input.ChangedFiles.Take(3))
                : "shared paths";
            Block(AuthorityReasonCodes.OverlappingReadyWorker,
                $"Blocked: overlapping worker diff ({overlapHint}).");
        }

        if (input.MergeObservabilityUnknown)
            Block(AuthorityReasonCodes.MergeObservabilityUnknown,
                "Blocked: merge conflict risk unknown (could not resolve changed files or merge state).");

        if (input.TaskRisk == RiskLevel.Critical)
            Block(AuthorityReasonCodes.CriticalRisk, "Blocked: critical-risk task.");

        if (input.UnknownHeadCommit)
            Block(AuthorityReasonCodes.UnknownHeadCommit, "Blocked: unknown head commit.");

        if (input.DirtyWorktree && input.Profile == AuthorityProfileKind.BalancedAuto)
            Block(AuthorityReasonCodes.DirtyWorktree, "Blocked: dirty worktree.");

        foreach (var (path, category) in protectedHits)
            Block(AuthorityReasonCodes.ProtectedPath, $"Blocked: touched protected path ({category}) {path}");

        if (files.Count == 0)
            Block(AuthorityReasonCodes.NoChangedFiles, "Blocked: no changed files known.");

        ApplyProfileLimits(input, files, protectedHits, risk, Block, codes);

        var autoAccept = codes.Count == 0;
        if (autoAccept)
            codes.Add(AuthorityReasonCodes.AutoAcceptEligible);

        var needsHuman = input.LeaseStatus == ExecutionLeaseStatus.ReadyForReview && !autoAccept;

        return new AuthorityDecision(
            input.Profile,
            risk,
            AutoAcceptAllowed: autoAccept,
            NeedsHumanReview: needsHuman,
            ReasonCodes: codes,
            HumanMessages: messages,
            ChangedFiles: files,
            ProtectedPaths: protectedHits.Select(h => h.Path).ToList());
    }

    private static void ApplyProfileLimits(
        AuthorityEvaluationInput input,
        IReadOnlyList<string> files,
        IReadOnlyList<(string Path, string Category)> protectedHits,
        AuthorityRiskLevel risk,
        Action<string, string> block,
        List<string> codes)
    {
        if (codes.Any(c => c is AuthorityReasonCodes.ProfileConservative or AuthorityReasonCodes.AutopilotDisabled))
            return;

        var count = input.ChangedFilesCount > 0 ? input.ChangedFilesCount : files.Count;

        switch (input.Profile)
        {
            case AuthorityProfileKind.BalancedAuto:
                if (input.TaskRisk >= RiskLevel.High)
                    block(AuthorityReasonCodes.HighRiskBalanced, "Blocked: high risk (Balanced-Auto requires low/medium).");
                if (count > input.LargeChangeSetThreshold)
                    block(AuthorityReasonCodes.LargeChangeSet,
                        $"Blocked: large change set ({count} files, limit {input.LargeChangeSetThreshold}).");
                break;

            case AuthorityProfileKind.Yolo:
                if (count > 50)
                    block(AuthorityReasonCodes.LargeChangeSet, $"Blocked: change set too large ({count} files, YOLO limit 50).");
                break;

            case AuthorityProfileKind.Custom:
                var custom = input.CustomRules ?? new CustomAuthorityRules();
                if (count > custom.MaxChangedFiles)
                    block(AuthorityReasonCodes.LargeChangeSet,
                        $"Blocked: over custom file limit ({custom.MaxChangedFiles}).");
                if (custom.AllowedRiskLevels.Count > 0
                    && !custom.AllowedRiskLevels.Contains(input.TaskRisk.ToString(), StringComparer.OrdinalIgnoreCase))
                    block(AuthorityReasonCodes.HighRiskBalanced, "Blocked: task risk not in custom allow list.");
                foreach (var path in files)
                {
                    if (custom.DenyPathGlobs.Count > 0
                        && ProtectedPathMatcher.MatchesAnyGlob(path, custom.DenyPathGlobs))
                        block(AuthorityReasonCodes.CustomDeny, $"Blocked: custom deny rule matched {path}");
                }

                if (custom.AllowPathGlobs.Count > 0)
                {
                    var unmatched = files
                        .Where(p => !ProtectedPathMatcher.MatchesAnyGlob(p, custom.AllowPathGlobs))
                        .ToList();
                    if (unmatched.Count > 0)
                        block(AuthorityReasonCodes.CustomAllowRequired,
                            $"Blocked: files outside custom allow list ({unmatched[0]}).");
                }

                break;
        }

        _ = protectedHits;
        _ = risk;
    }

    private static AuthorityRiskLevel MapRisk(RiskLevel taskRisk, int fileCount, bool hasProtected) =>
        taskRisk switch
        {
            RiskLevel.Critical => AuthorityRiskLevel.Critical,
            RiskLevel.High => AuthorityRiskLevel.High,
            RiskLevel.Medium => AuthorityRiskLevel.Medium,
            _ => fileCount > 15 || hasProtected ? AuthorityRiskLevel.Medium : AuthorityRiskLevel.Low,
        };

    public static AuthorityProfileKind ResolveProfile(
        AuthorityOptions options,
        Guid sessionId,
        string sessionName)
    {
        if (options.SessionProfileOverrides.TryGetValue(sessionId.ToString(), out var byId))
            return byId;
        if (options.SessionProfileOverrides.TryGetValue(sessionName, out var byName))
            return byName;
        return options.DefaultProfile;
    }
}
