using JoyZoning.Domain.Orchestration;

namespace JoyZoning.ControlPlane.Services;

public static class WorkspaceLiveResponseBuilder
{
    public static object ToApiBody(WorkspaceLiveSnapshot snapshot)
    {
        var display = snapshot.Presentation;

        return new
        {
            taskId = snapshot.TaskId,
            title = snapshot.TaskTitle,
            leaseStatus = snapshot.LeaseStatus,
            blockedReason = snapshot.BlockedReason,
            worktreePath = snapshot.WorktreePath,
            sessionWorkspaceRoot = snapshot.SessionWorkspaceRoot,
            liveFile = snapshot.LiveFilePath,
            recommendedPollSeconds = display.RecommendedPollSeconds,
            progress = new
            {
                appScreens = snapshot.AppScreenCount,
                featureFiles = snapshot.FeatureFileCount,
                sharedFiles = snapshot.SharedFileCount,
                hasPackageJson = snapshot.HasPackageJson,
                hasReadme = snapshot.HasReadme,
                filesCopiedThisTick = snapshot.FilesCopiedThisTick,
                worktreeFileCount = snapshot.WorktreeFileCount,
                worktreeLastWriteUtc = snapshot.WorktreeLastWriteUtc,
                lastEvidenceKind = snapshot.LastEvidenceKind,
            },
            display = new
            {
                headline = display.Headline,
                subheadline = display.Subheadline,
                activityState = display.ActivityState,
                progressPercent = display.ProgressPercent,
                currentStepIndex = display.CurrentStepIndex,
                stepCount = display.StepCount,
                stepProgressLabel = display.StepProgressLabel,
                phaseLabel = display.PhaseLabel,
                navigationSummary = display.NavigationSummary,
                currentStepTitle = display.CurrentStepTitle,
                timeGuidance = display.TimeGuidance,
                staleWarning = display.StaleWarning,
                pollMode = display.PollMode,
                steps = display.Steps.Select(s => new { id = s.Id, label = s.Label, state = s.State }),
                deliverables = display.Deliverables.Select(d => new
                {
                    id = d.Id,
                    label = d.Label,
                    done = d.Done,
                    count = d.Count,
                }),
                nextActions = display.NextActions,
                recentActivity = display.RecentActivity,
                helpTips = display.HelpTips,
            },
            recentEvidence = snapshot.RecentEvidence,
            updatedAt = snapshot.UpdatedAt,
            watch = new
            {
                command = $"jz task watch {snapshot.TaskId}",
                statusMarkdown = snapshot.LiveFilePath,
                statusJson = Path.Combine(snapshot.SessionWorkspaceRoot, ".joyzoning/live.json"),
            },
        };
    }

    public static object IdleBody(Guid taskId, string title, string message) => new
    {
        taskId,
        title,
        message,
        leaseStatus = (string?)null,
        liveFile = (string?)null,
        recommendedPollSeconds = 15,
        display = WorkspaceLivePresentation.Build(
            title,
            leaseStatus: null,
            blockedReason: null,
            evidenceLogJson: null,
            appScreens: 0,
            featureFiles: 0,
            sharedFiles: 0,
            hasPackageJson: false,
            hasReadme: false,
            worktreeFileCount: 0,
            worktreeLastWriteUtc: null,
            filesCopiedThisTick: 0),
    };
}
