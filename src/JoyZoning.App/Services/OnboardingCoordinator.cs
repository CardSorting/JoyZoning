using JoyZoning.App.Services.Onboarding;

namespace JoyZoning.App.Services;

/// <summary>Central onboarding orchestration (assessment, smart setup, preferences).</summary>
public static class OnboardingCoordinator
{
    public static async Task<OnboardingAssessment> AssessAsync(
        bool hasActiveSession,
        bool hermesFullyReady,
        CancellationToken cancellationToken = default)
    {
        var snap = await OnboardingEvaluator.AssessAsync(hasActiveSession, cancellationToken);
        var diagnostics = OnboardingEvaluator.BuildDiagnostics(snap, hermesFullyReady);
        var steps = OnboardingEvaluator.BuildStepStates(snap, hasActiveSession);
        var phase = ResolvePhase(snap, hasActiveSession);
        var incompleteRequired = steps.Where(s => s.Definition.IsRequired && !s.IsComplete).ToList();
        var minutesLeft = incompleteRequired.Sum(s => s.Definition.EstimatedMinutes);

        var next = incompleteRequired.OrderBy(s => s.Definition.Order).FirstOrDefault();
        var headline = snap.ProgressPercent >= 100
            ? "You're ready to operate"
            : next?.Definition.Title ?? "Finish setup";
        var subheadline = snap.Message;
        var primaryLabel = next?.Definition.FixLabel ?? "Review setup";
        var primaryId = next?.Definition.Id;

        return new OnboardingAssessment(
            snap,
            steps,
            diagnostics,
            phase,
            headline,
            subheadline,
            primaryLabel,
            primaryId,
            minutesLeft,
            snap.ProgressPercent >= 100);
    }

    public static OnboardingJourneyPhase ResolvePhase(OnboardingSnapshot snap, bool hasSession)
    {
        if (snap.ProgressPercent >= 100)
            return OnboardingJourneyPhase.Complete;
        if (!snap.ControlPlaneOk || !snap.PathsValid)
            return OnboardingJourneyPhase.Configure;
        if (!snap.GatewayOk || !snap.DashboardOk)
            return OnboardingJourneyPhase.Connect;
        if (!hasSession)
            return OnboardingJourneyPhase.Operate;
        return OnboardingJourneyPhase.Complete;
    }

    public static SurfaceCoachMark? CoachMarkFor(string surfaceId) =>
        OnboardingCatalog.SurfaceCoachMarks.FirstOrDefault(m =>
            string.Equals(m.SurfaceId, surfaceId, StringComparison.OrdinalIgnoreCase));

    public static bool ShouldShowCoachMark(string surfaceId)
    {
        var prefs = OnboardingPreferences.Load();
        return !prefs.DismissedSurfaceTips.Contains(surfaceId, StringComparer.OrdinalIgnoreCase);
    }

    public static void DismissCoachMark(string surfaceId)
    {
        var prefs = OnboardingPreferences.Load();
        if (!prefs.DismissedSurfaceTips.Contains(surfaceId, StringComparer.OrdinalIgnoreCase))
        {
            prefs.DismissedSurfaceTips.Add(surfaceId);
            prefs.Save();
        }
    }

    public static void MarkCelebrationShown()
    {
        var prefs = OnboardingPreferences.Load();
        prefs.CelebrationShownAt = DateTimeOffset.UtcNow;
        prefs.Save();
    }

    public static bool ShouldShowCelebration(bool isComplete)
    {
        if (!isComplete) return false;
        var prefs = OnboardingPreferences.Load();
        return prefs.CelebrationShownAt is null;
    }

    public static void RecordFirstLaunch()
    {
        var prefs = OnboardingPreferences.Load();
        if (prefs.FirstLaunchAt is null)
        {
            prefs.FirstLaunchAt = DateTimeOffset.UtcNow;
            prefs.Save();
        }
    }

    public static void RecordHubVisit()
    {
        var prefs = OnboardingPreferences.Load();
        prefs.LastHubVisitAt = DateTimeOffset.UtcNow;
        prefs.Save();
    }

    /// <summary>Persist completion timestamps when live checks pass (Stripe-style activation tracking).</summary>
    public static void SyncCoreStepCompletions(OnboardingSnapshot snap, bool hasSession)
    {
        var prefs = OnboardingPreferences.Load();
        var changed = false;

        void TryMark(string id, bool done)
        {
            if (!done) return;
            if (!prefs.StepCompletedAt.ContainsKey(id))
            {
                prefs.RecordStepCompleted(id);
                changed = true;
            }
        }

        TryMark(OnboardingStepIds.ControlPlane, snap.ControlPlaneOk);
        TryMark(OnboardingStepIds.CliPaths, snap.CliOk);
        TryMark(OnboardingStepIds.Gateway, snap.GatewayOk);
        TryMark(OnboardingStepIds.Dashboard, snap.DashboardOk);
        TryMark(OnboardingStepIds.Workspace, hasSession);

        if (snap.ProgressPercent >= 100 && prefs.CoreSetupCompletedAt is null)
        {
            prefs.CoreSetupCompletedAt = DateTimeOffset.UtcNow;
            changed = true;
        }

        if (changed)
            prefs.Save();
    }

    public static OnboardingReportContext BuildReportContext(
        bool hasSession,
        bool hermesFullyReady,
        string hermesStatus,
        string dashboardStatus,
        string projectName) =>
        new(hasSession, hermesFullyReady, hermesStatus, dashboardStatus, projectName);
}
