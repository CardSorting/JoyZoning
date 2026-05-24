namespace JoyZoning.Domain.Events;

/// <summary>Canonical JoyZoning event type identifiers.</summary>
public static class EventTypes
{
    public const string SessionStarted = "session.started";
    public const string SessionEnded = "session.ended";
    public const string TaskCreated = "task.created";
    public const string KanbanImported = "kanban.imported";
    public const string KanbanSynced = "kanban.synced";
    public const string TaskStatusChanged = "task.status_changed";
    public const string HermesRunStarted = "hermes.run.started";
    public const string HermesRunCompleted = "hermes.run.completed";
    public const string HermesMessageDelta = "hermes.message.delta";
    public const string HermesToolStarted = "hermes.tool.started";
    public const string HermesToolCompleted = "hermes.tool.completed";
    public const string HermesApprovalRequested = "hermes.approval.requested";
    public const string HermesApprovalResolved = "hermes.approval.resolved";
    public const string DietCodeExecutionStarted = "dietcode.execution.started";
    public const string DietCodeExecutionCompleted = "dietcode.execution.completed";
    public const string ExecutionLeaseCreated = "execution.lease.created";
    public const string ExecutionLeaseStatusChanged = "execution.lease.status_changed";
    public const string ExecutionLeaseRevoked = "execution.lease.revoked";
    public const string ExecutionLeaseMerged = "execution.lease.merged";
    public const string VerificationReportAttached = "verification.report.attached";
    public const string TerminalOutput = "terminal.output";
    public const string WorkspaceFileChanged = "workspace.file.changed";
    public const string GitStatusChanged = "git.status.changed";
    public const string ApprovalGranted = "approval.granted";
    public const string ApprovalDenied = "approval.denied";
    public const string ExternalWorkStarted = "external.work.started";
    public const string ExternalAgentPromptGenerated = "external.agent.prompt_generated";
    public const string ExternalWorkspaceStatusScanned = "external.workspace.status_scanned";
    public const string ExternalWorkMarkedReadyForReview = "external.work.marked_ready_for_review";
    public const string ExternalVerificationStarted = "external.verification.started";
    public const string ExternalVerificationPassed = "external.verification.passed";
    public const string ExternalVerificationFailed = "external.verification.failed";
    public const string ExternalWorkMerged = "external.work.merged";
    public const string ExternalWorkCompleted = "external.work.completed";
}
