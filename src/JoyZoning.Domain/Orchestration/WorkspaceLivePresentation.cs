using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

/// <summary>
/// Human-friendly progress narrative for live workspace mirroring (terminal + markdown + API).
/// </summary>
public static class WorkspaceLivePresentation
{
    public const string StepPending = "pending";
    public const string StepCurrent = "current";
    public const string StepComplete = "complete";
    public const string StepFailed = "failed";

    public const string ActivityWaiting = "waiting";
    public const string ActivityActive = "active";
    public const string ActivityIdle = "idle";
    public const string ActivityStuck = "stuck";
    public const string ActivityBlocked = "blocked";
    public const string ActivityReview = "review";
    public const string ActivityDone = "done";
    public const string ActivityNone = "none";

    public sealed record Step(string Id, string Label, string State);

    public sealed record Deliverable(string Id, string Label, bool Done, int? Count);

    public sealed record ViewModel(
        string Headline,
        string Subheadline,
        string ActivityState,
        int ProgressPercent,
        int CurrentStepIndex,
        int StepCount,
        string StepProgressLabel,
        string? CurrentStepTitle,
        string? TimeGuidance,
        string? StaleWarning,
        string PollMode,
        IReadOnlyList<Step> Steps,
        IReadOnlyList<Deliverable> Deliverables,
        IReadOnlyList<string> NextActions,
        IReadOnlyList<string> RecentActivity,
        IReadOnlyList<string> HelpTips,
        int RecommendedPollSeconds);

    public const string PollStopped = "stopped";
    public const string PollBurst = "burst";
    public const string PollNormal = "normal";
    public const string PollSlow = "slow";

    public static ViewModel Build(
        string? taskTitle,
        string? leaseStatus,
        string? blockedReason,
        string? evidenceLogJson,
        int appScreens,
        int featureFiles,
        int sharedFiles,
        bool hasPackageJson,
        bool hasReadme,
        int worktreeFileCount,
        DateTimeOffset? worktreeLastWriteUtc,
        int filesCopiedThisTick,
        DateTimeOffset? now = null)
    {
        var at = now ?? DateTimeOffset.UtcNow;
        var status = leaseStatus ?? "";
        var entries = LeaseEvidenceLog.DeserializeEntries(evidenceLogJson);
        var recentActivity = BuildRecentActivity(entries);
        var deliverables = BuildDeliverables(
            hasPackageJson, hasReadme, appScreens, featureFiles, sharedFiles);
        var steps = BuildSteps(status);
        var currentIndex = CurrentStepIndex(status);
        var activity = ClassifyActivity(status, worktreeLastWriteUtc, blockedReason, at);
        var percent = ComputeProgressPercent(status, currentIndex, deliverables);
        var (headline, subheadline) = BuildHeadlines(
            taskTitle, status, activity, blockedReason, worktreeLastWriteUtc, filesCopiedThisTick, at);
        var nextActions = BuildNextActions(status, blockedReason, activity);
        var (poll, pollMode) = RecommendPoll(status, worktreeLastWriteUtc, filesCopiedThisTick, activity, at);
        var stepLabel = FormatStepProgressLabel(currentIndex, steps.Count);
        var currentStepTitle = steps.ElementAtOrDefault(currentIndex)?.Label;
        var timeGuidance = BuildTimeGuidance(status, activity);
        var staleWarning = BuildStaleWarning(activity, worktreeLastWriteUtc, at);
        var helpTips = BuildHelpTips(activity, status);

        return new ViewModel(
            headline,
            subheadline,
            activity,
            percent,
            currentIndex,
            steps.Count,
            stepLabel,
            currentStepTitle,
            timeGuidance,
            staleWarning,
            pollMode,
            steps,
            deliverables,
            nextActions,
            recentActivity,
            helpTips,
            poll);
    }

    public static int RecommendPollSeconds(
        string leaseStatus,
        DateTimeOffset? worktreeLastWriteUtc,
        int filesCopiedThisTick,
        string? activityState = null,
        DateTimeOffset? now = null) =>
        RecommendPoll(leaseStatus, worktreeLastWriteUtc, filesCopiedThisTick, activityState, now).Seconds;

    public static (int Seconds, string Mode) RecommendPoll(
        string leaseStatus,
        DateTimeOffset? worktreeLastWriteUtc,
        int filesCopiedThisTick,
        string? activityState = null,
        DateTimeOffset? now = null)
    {
        var at = now ?? DateTimeOffset.UtcNow;
        activityState ??= ClassifyActivity(leaseStatus, worktreeLastWriteUtc, null, at);

        var baseSeconds = leaseStatus switch
        {
            nameof(ExecutionLeaseStatus.Running) => 5,
            nameof(ExecutionLeaseStatus.Leased) => 8,
            nameof(ExecutionLeaseStatus.Verifying) => 12,
            nameof(ExecutionLeaseStatus.Blocked) => 25,
            nameof(ExecutionLeaseStatus.ReadyForReview) => 0,
            nameof(ExecutionLeaseStatus.Merged) or nameof(ExecutionLeaseStatus.Revoked) => 0,
            _ => 10,
        };

        if (baseSeconds == 0)
            return (0, PollStopped);

        if (filesCopiedThisTick > 0)
            return (Math.Min(baseSeconds, 3), PollBurst);

        if (activityState == ActivityActive)
            return (Math.Min(baseSeconds, 3), PollBurst);

        if (activityState is ActivityStuck or ActivityIdle)
            return (Math.Max(baseSeconds, 20), PollSlow);

        if (activityState == ActivityBlocked)
            return (Math.Max(baseSeconds, 25), PollSlow);

        return (baseSeconds, PollNormal);
    }

    public static string FormatStepProgressLabel(int currentIndex, int stepCount)
    {
        var human = Math.Clamp(currentIndex + 1, 1, Math.Max(stepCount, 1));
        return $"Step {human} of {Math.Max(stepCount, 1)}";
    }

    private static string? BuildTimeGuidance(string status, string activity) => activity switch
    {
        ActivityNone => "Dispatch a task to start. First progress usually appears within a minute.",
        ActivityWaiting => "Starting the AI worker typically takes 1–3 minutes.",
        ActivityActive => "Active builds often take 10–30 minutes depending on app size.",
        ActivityIdle => "The AI may be thinking or running tools — quiet periods are normal for a few minutes.",
        ActivityStuck => "If nothing changes for 10+ minutes, check the tips below or Hermes logs.",
        ActivityBlocked => "Fix the issue below, then recover and run the task again.",
        ActivityReview => "Review the app in your project folder when you're ready.",
        ActivityDone => null,
        _ when status == nameof(ExecutionLeaseStatus.Verifying) => "Automated checks usually finish in 1–5 minutes.",
        _ => "Most app builds take 10–30 minutes end-to-end.",
    };

    private static string? BuildStaleWarning(
        string activity,
        DateTimeOffset? worktreeLastWriteUtc,
        DateTimeOffset now)
    {
        if (activity is not (ActivityIdle or ActivityStuck))
            return null;

        if (worktreeLastWriteUtc is null)
            return "No project files detected yet — the worker may still be starting.";

        var age = now - worktreeLastWriteUtc.Value;
        if (activity == ActivityStuck)
            return $"No file changes for {FormatFriendlyDuration(age)}. Long AI steps can look quiet — wait a bit or check logs.";

        return null;
    }

    private static IReadOnlyList<string> BuildHelpTips(string activity, string status)
    {
        var tips = new List<string>
        {
            "Your app appears in the project folder as files are written — open JOYZONING_LIVE.md anytime.",
            "You can close this window; the build continues. Re-run the watch command to check back.",
        };

        if (activity is ActivityActive or ActivityIdle or ActivityStuck)
            tips.Add("Quiet minutes are normal while the AI plans or runs tools.");

        if (activity == ActivityBlocked)
            tips.Add("After fixing credentials or credits, use recover + run — you do not need to delete the project.");

        if (status == nameof(ExecutionLeaseStatus.ReadyForReview))
            tips.Add("Nothing merges automatically — you approve when the app looks right.");

        return tips;
    }

    public static string ClassifyActivity(
        string leaseStatus,
        DateTimeOffset? worktreeLastWriteUtc,
        string? blockedReason,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(leaseStatus))
            return ActivityNone;

        if (leaseStatus == nameof(ExecutionLeaseStatus.Blocked) || !string.IsNullOrWhiteSpace(blockedReason))
            return ActivityBlocked;

        if (leaseStatus == nameof(ExecutionLeaseStatus.ReadyForReview))
            return ActivityReview;

        if (leaseStatus is nameof(ExecutionLeaseStatus.Merged) or nameof(ExecutionLeaseStatus.Revoked))
            return ActivityDone;

        if (leaseStatus == nameof(ExecutionLeaseStatus.Leased))
            return ActivityWaiting;

        if (leaseStatus is not (nameof(ExecutionLeaseStatus.Running) or nameof(ExecutionLeaseStatus.Verifying)))
            return ActivityWaiting;

        if (worktreeLastWriteUtc is null)
            return ActivityIdle;

        var age = now - worktreeLastWriteUtc.Value;
        if (age <= TimeSpan.FromSeconds(90))
            return ActivityActive;
        if (age <= TimeSpan.FromMinutes(5))
            return ActivityIdle;

        return ActivityStuck;
    }

    private static IReadOnlyList<Step> BuildSteps(string status)
    {
        var pipeline = new[]
        {
            ("start", "Getting started"),
            ("build", "Building your project"),
            ("verify", "Running quality checks"),
            ("review", "Ready for your review"),
        };

        var current = CurrentStepIndex(status);
        var failed = status == nameof(ExecutionLeaseStatus.Blocked);

        var steps = new List<Step>();
        for (var i = 0; i < pipeline.Length; i++)
        {
            string state;
            if (failed && i == current)
                state = StepFailed;
            else if (i < current)
                state = StepComplete;
            else if (i == current)
                state = StepCurrent;
            else
                state = StepPending;

            steps.Add(new Step(pipeline[i].Item1, pipeline[i].Item2, state));
        }

        if (status is nameof(ExecutionLeaseStatus.Merged))
            return pipeline.Select(p => new Step(p.Item1, p.Item2, StepComplete)).ToList();

        return steps;
    }

    private static int CurrentStepIndex(string status) => status switch
    {
        nameof(ExecutionLeaseStatus.Leased) => 0,
        nameof(ExecutionLeaseStatus.Running) => 1,
        nameof(ExecutionLeaseStatus.Blocked) => 1,
        nameof(ExecutionLeaseStatus.Verifying) => 2,
        nameof(ExecutionLeaseStatus.ReadyForReview) => 3,
        nameof(ExecutionLeaseStatus.Merged) => 3,
        _ => 0,
    };

    private static int ComputeProgressPercent(string status, int stepIndex, IReadOnlyList<Deliverable> deliverables)
    {
        var stepWeight = 70;
        var deliverableWeight = 30;
        var stepPercent = status switch
        {
            nameof(ExecutionLeaseStatus.Merged) => 100,
            nameof(ExecutionLeaseStatus.ReadyForReview) => 90,
            _ => (int)Math.Round((stepIndex + 0.5) / 4.0 * stepWeight),
        };

        var done = deliverables.Count(d => d.Done);
        var delPercent = deliverables.Count == 0
            ? 0
            : (int)Math.Round(done / (double)deliverables.Count * deliverableWeight);

        return Math.Clamp(stepPercent + delPercent, 0, 100);
    }

    private static IReadOnlyList<Deliverable> BuildDeliverables(
        bool hasPackageJson,
        bool hasReadme,
        int appScreens,
        int featureFiles,
        int sharedFiles) =>
    [
        new("package", "Project setup", hasPackageJson, null),
        new("screens", "App screens", appScreens > 0, appScreens),
        new("features", "App features", featureFiles > 0, featureFiles),
        new("shared", "Shared code", sharedFiles > 0, sharedFiles),
        new("readme", "README guide", hasReadme, null),
    ];

    private static (string Headline, string Subheadline) BuildHeadlines(
        string? taskTitle,
        string status,
        string activity,
        string? blockedReason,
        DateTimeOffset? lastWrite,
        int filesCopied,
        DateTimeOffset now)
    {
        var name = string.IsNullOrWhiteSpace(taskTitle) ? "Your project" : taskTitle.Trim();

        if (activity == ActivityBlocked)
        {
            var hint = SummarizeBlockedReason(blockedReason);
            return ($"Paused — needs your attention", $"{name}: {hint}");
        }

        if (activity == ActivityReview)
            return ("Ready for you to review", $"{name} finished building. Take a look when you can.");

        if (activity == ActivityDone)
            return ("All done", $"{name} has been merged.");

        if (activity == ActivityNone)
            return ("Waiting to start", "No active build right now. Dispatch a task to begin.");

        if (status == nameof(ExecutionLeaseStatus.Verifying))
            return ("Checking the build", $"{name} — running automated checks.");

        if (activity == ActivityStuck)
            return ("Still working, but slow", $"{name} — no new files for a few minutes. This can be normal for long AI steps.");

        if (activity == ActivityActive)
        {
            var sub = filesCopied > 0
                ? $"{filesCopied} file(s) just synced to your project folder."
                : "Files are being updated in your project folder.";
            return ($"Building {name}", sub);
        }

        if (status == nameof(ExecutionLeaseStatus.Leased))
            return ("Starting up", $"{name} — preparing the AI worker.");

        if (lastWrite is { } w)
        {
            var ago = FormatFriendlyDuration(now - w);
            return ($"Building {name}", $"Last file change {ago}.");
        }

        return ($"Working on {name}", "JoyZoning is orchestrating the build.");
    }

    private static IReadOnlyList<string> BuildNextActions(
        string status,
        string? blockedReason,
        string activity)
    {
        var actions = new List<string>();

        if (activity == ActivityBlocked)
        {
            var reason = (blockedReason ?? "").ToLowerInvariant();
            if (reason.Contains("401") || reason.Contains("api key") || reason.Contains("api_key")
                || reason.Contains("invalid_api") || reason.Contains("invalid key"))
            {
                actions.Add("Check your AI provider API key (Hermes profile: joyzoning).");
                actions.Add("Run: hermes -p joyzoning config show");
                actions.Add("Sync keys: curl -X POST http://127.0.0.1:9470/api/hermes/sync-credentials");
            }
            else if (reason.Contains("credit") || reason.Contains("balance") || reason.Contains("fund"))
            {
                actions.Add("Add credits or upgrade your AI provider plan.");
            }
            else
            {
                actions.Add("Read the blocked reason below and fix the underlying issue.");
            }

            actions.Add("Recover the task: jz task recover <task-id> --mode reopen");
            actions.Add("Resume: jz task run <task-id> --poll 20");
            return actions;
        }

        if (status == nameof(ExecutionLeaseStatus.ReadyForReview))
        {
            actions.Add("Open the project folder in your editor and review the app.");
            actions.Add("When satisfied: jz task complete <task-id> --yes");
            return actions;
        }

        if (activity == ActivityStuck)
        {
            actions.Add("If this persists >10 minutes, check Hermes logs: hermes -p joyzoning logs --follow");
            actions.Add("You can stop and recover: jz task recover <task-id> --mode reopen");
        }

        if (status is nameof(ExecutionLeaseStatus.Running) or nameof(ExecutionLeaseStatus.Leased))
        {
            actions.Add("Keep this window open for live updates, or open JOYZONING_LIVE.md in your project folder.");
        }

        return actions;
    }

    private static IReadOnlyList<string> BuildRecentActivity(IReadOnlyList<LeaseEvidenceEntry> entries)
    {
        var lines = new List<string>();
        foreach (var e in entries.TakeLast(5))
        {
            var text = FriendlyEvidence(e);
            if (!string.IsNullOrWhiteSpace(text))
                lines.Add(text);
        }

        return lines;
    }

    public static string FriendlyEvidence(LeaseEvidenceEntry entry)
    {
        var summary = entry.Summary?.Trim();
        var kind = entry.Kind ?? "";

        var label = kind switch
        {
            "dispatch.failed" => "Could not start the AI worker",
            "dispatch.retry" => "Retrying dispatch",
            "recovery.reopen" => "Task reopened after a fix",
            "recovery.reattach" => "Reconnected to existing work",
            "execution.completed" => "Build step completed",
            "lease.expired_blocked" => "Lease timed out",
            "verification.submitted" => "Verification submitted",
            _ when kind.Contains("blocked", StringComparison.OrdinalIgnoreCase) => "Build paused",
            _ => kind.Replace('.', ' ').Replace('_', ' '),
        };

        if (!string.IsNullOrWhiteSpace(summary))
            return $"{label}: {summary}";

        return label;
    }

    public static string SummarizeBlockedReason(string? blockedReason)
    {
        if (string.IsNullOrWhiteSpace(blockedReason))
            return "Something needs to be fixed before the build can continue.";

        var r = blockedReason.ToLowerInvariant();
        if (r.Contains("401") || r.Contains("api_key") || r.Contains("api key")
            || (r.Contains("invalid") && r.Contains("key")))
            return "Your AI provider rejected the API key.";
        if (r.Contains("credit") || r.Contains("balance") || r.Contains("insufficient"))
            return "Your AI provider account needs more credits.";
        if (r.Contains("timeout"))
            return "The AI worker timed out.";
        if (r.Contains("rate limit"))
            return "The AI provider rate-limited requests — wait a moment and retry.";

        return blockedReason.Length > 160 ? blockedReason[..157] + "…" : blockedReason;
    }

    private static string FormatFriendlyDuration(TimeSpan span)
    {
        if (span.TotalSeconds < 60)
            return $"{(int)span.TotalSeconds}s ago";
        if (span.TotalMinutes < 60)
            return $"{(int)span.TotalMinutes}m ago";
        return $"{(int)span.TotalHours}h ago";
    }
}
