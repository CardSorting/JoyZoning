namespace JoyZoning.App.Services.Onboarding;

public enum OnboardingJourneyPhase
{
    NotStarted,
    Configure,
    Connect,
    Operate,
    Complete,
}

public enum OnboardingDiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

public record OnboardingDiagnostic(
    string Code,
    string Title,
    string Detail,
    OnboardingDiagnosticSeverity Severity,
    string? FixStepId = null);

public record OnboardingStepDefinition(
    string Id,
    int Order,
    string Title,
    string Description,
    string WhyItMatters,
    string FixLabel,
    int EstimatedMinutes,
    string? LearnMoreUrl,
    string? NavigateSurfaceId,
    bool IsRequired = true);

public record OnboardingStepState(
    OnboardingStepDefinition Definition,
    bool IsComplete,
    bool IsBlocked,
    string? BlockedReason,
    string? RequiresLabel = null);

public record OnboardingSnapshot(
    bool ControlPlaneOk,
    bool CliOk,
    bool GatewayOk,
    bool DashboardOk,
    bool WorkspaceOk,
    bool PathsValid,
    int ProgressPercent,
    string Message);

public record OnboardingAssessment(
    OnboardingSnapshot Snapshot,
    IReadOnlyList<OnboardingStepState> Steps,
    IReadOnlyList<OnboardingDiagnostic> Diagnostics,
    OnboardingJourneyPhase Phase,
    string Headline,
    string Subheadline,
    string PrimaryActionLabel,
    string? PrimaryActionStepId,
    int EstimatedMinutesRemaining,
    bool IsComplete);

public record OnboardingWhatsNextCard(
    string Title,
    string Description,
    string SurfaceId,
    string ActionLabel);

public record SurfaceCoachMark(
    string SurfaceId,
    string Title,
    string Body,
    string? LearnMoreUrl);

public record OnboardingQuickAction(string Id, string Title, string Description);

public record OnboardingResourceLink(
    string Title,
    string Description,
    string UrlOrPath,
    bool IsLocalDoc = false);

public record OnboardingPlaybook(
    string Title,
    string Summary,
    IReadOnlyList<string> Steps,
    string? RelatedStepId);
