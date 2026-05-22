using System.Text;

namespace JoyZoning.App.Services.Onboarding;

public enum OnboardingHealthGrade
{
    Healthy,
    Degraded,
    Blocked,
}

public static class OnboardingHealthReport
{
    public static OnboardingHealthGrade Grade(OnboardingAssessment assessment)
    {
        if (assessment.IsComplete)
            return OnboardingHealthGrade.Healthy;

        if (assessment.Diagnostics.Any(d => d.Severity == OnboardingDiagnosticSeverity.Error))
            return OnboardingHealthGrade.Blocked;

        if (assessment.Diagnostics.Any(d => d.Severity == OnboardingDiagnosticSeverity.Warning)
            || assessment.Snapshot.ProgressPercent > 0)
            return OnboardingHealthGrade.Degraded;

        return OnboardingHealthGrade.Blocked;
    }

    public static string GradeLabel(OnboardingHealthGrade grade) => grade switch
    {
        OnboardingHealthGrade.Healthy => "Healthy",
        OnboardingHealthGrade.Degraded => "Degraded",
        _ => "Blocked",
    };

    public static string GradeColor(OnboardingHealthGrade grade) => grade switch
    {
        OnboardingHealthGrade.Healthy => "#3fb950",
        OnboardingHealthGrade.Degraded => "#d29922",
        _ => "#f85149",
    };

    public static async Task<string> BuildAsync(
        OnboardingReportContext ctx,
        CancellationToken cancellationToken = default)
    {
        var assessment = await OnboardingCoordinator.AssessAsync(
            ctx.HasActiveSession,
            ctx.HermesFullyReady,
            cancellationToken);

        var prefs = OnboardingPreferences.Load();
        var settings = await AppServices.ControlPlane.GetSettingsAsync();
        var sb = new StringBuilder();

        sb.AppendLine("JoyZoning Onboarding Health Report");
        sb.AppendLine($"Generated: {DateTimeOffset.Now:u}");
        sb.AppendLine($"Grade: {GradeLabel(Grade(assessment))} ({assessment.Snapshot.ProgressPercent}%)");
        sb.AppendLine($"Phase: {OnboardingCatalog.PhaseLabel(assessment.Phase)}");
        sb.AppendLine();

        sb.AppendLine("## Environment");
        sb.AppendLine($"OS: {Environment.OSVersion}");
        sb.AppendLine($"Control plane: {(assessment.Snapshot.ControlPlaneOk ? "OK" : "OFFLINE")}");
        sb.AppendLine($"Hermes API: {ctx.HermesStatus}");
        sb.AppendLine($"Dashboard: {ctx.DashboardStatus}");
        sb.AppendLine($"Install root: {settings?.InstallRoot ?? "(not set)"}");
        sb.AppendLine($"API URL: {settings?.ApiBaseUrl}");
        sb.AppendLine($"Dashboard URL: {settings?.DashboardBaseUrl}");
        sb.AppendLine($"Workspace session: {(ctx.HasActiveSession ? ctx.ProjectName : "none")}");
        sb.AppendLine();

        sb.AppendLine("## Core checklist");
        foreach (var step in assessment.Steps)
        {
            var mark = step.IsComplete ? "[x]" : "[ ]";
            sb.AppendLine($"{mark} {step.Definition.Order}. {step.Definition.Title}");
            if (step.IsBlocked && step.BlockedReason is { Length: > 0 } reason)
                sb.AppendLine($"    blocked: {reason}");
            var completedAt = prefs.GetStepCompletedAt(step.Definition.Id);
            if (completedAt is not null)
                sb.AppendLine($"    completed: {completedAt:u}");
        }

        sb.AppendLine();
        if (assessment.Diagnostics.Count > 0)
        {
            sb.AppendLine("## Diagnostics");
            foreach (var d in assessment.Diagnostics)
                sb.AppendLine($"- [{d.Severity}] {d.Title}: {d.Detail}");
            sb.AppendLine();
        }

        sb.AppendLine("## Suggested next action");
        sb.AppendLine($"{assessment.PrimaryActionLabel} (step: {assessment.PrimaryActionStepId ?? "n/a"})");
        sb.AppendLine(assessment.Subheadline);

        return sb.ToString();
    }
}

public record OnboardingReportContext(
    bool HasActiveSession,
    bool HermesFullyReady,
    string HermesStatus,
    string DashboardStatus,
    string ProjectName);
