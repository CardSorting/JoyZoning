using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;

namespace JoyZoning.Cli;

/// <summary>Subset of control-plane operations YOLO may invoke.</summary>
public interface IYoloRunClient
{
    Task<CliHttpResult> ListTasksAsync(Guid sessionId);
    Task<CliHttpResult> GetLeaseAsync(Guid taskId);
    Task<CliHttpResult> DispatchTaskAsync(Guid taskId, bool humanApprovedCritical);
    Task<CliHttpResult> DispatchRetryAsync(
        Guid taskId,
        Guid sessionId,
        StatusChangeActor actor,
        bool humanApprovedCritical);
    Task<CliHttpResult> RecoverLeaseAsync(
        Guid taskId,
        Guid sessionId,
        LeaseRecoveryMode mode,
        StatusChangeActor actor,
        bool humanApprovedCritical);
    Task<CliHttpResult> HeartbeatLeaseAsync(Guid taskId, Guid sessionId, StatusChangeActor actor);
    Task<CliHttpResult> AgentLeaseStatusAsync(Guid taskId, ExecutionLeaseStatus status, string? reason);
    Task<CliHttpResult> SubmitVerificationAsync(Guid taskId, VerificationReport report, bool supersede);
    Task<CliHttpResult> RecordAgentEvidenceAsync(Guid taskId, string kind, string summary, object? detail = null);
}

public sealed class YoloRunClientAdapter(JoyZoningCliClient inner) : IYoloRunClient
{
    public Task<CliHttpResult> ListTasksAsync(Guid sessionId) => inner.ListTasksAsync(sessionId);
    public Task<CliHttpResult> GetLeaseAsync(Guid taskId) => inner.GetLeaseAsync(taskId);
    public Task<CliHttpResult> DispatchTaskAsync(Guid taskId, bool humanApprovedCritical) =>
        inner.DispatchTaskAsync(taskId, humanApprovedCritical);
    public Task<CliHttpResult> DispatchRetryAsync(
        Guid taskId,
        Guid sessionId,
        StatusChangeActor actor,
        bool humanApprovedCritical) =>
        inner.DispatchRetryAsync(taskId, sessionId, actor, humanApprovedCritical);
    public Task<CliHttpResult> RecoverLeaseAsync(
        Guid taskId,
        Guid sessionId,
        LeaseRecoveryMode mode,
        StatusChangeActor actor,
        bool humanApprovedCritical) =>
        inner.RecoverLeaseAsync(taskId, sessionId, mode, actor, humanApprovedCritical);
    public Task<CliHttpResult> HeartbeatLeaseAsync(Guid taskId, Guid sessionId, StatusChangeActor actor) =>
        inner.HeartbeatLeaseAsync(taskId, sessionId, actor);
    public Task<CliHttpResult> AgentLeaseStatusAsync(Guid taskId, ExecutionLeaseStatus status, string? reason) =>
        inner.AgentLeaseStatusAsync(taskId, status, reason);
    public Task<CliHttpResult> SubmitVerificationAsync(Guid taskId, VerificationReport report, bool supersede) =>
        inner.SubmitVerificationAsync(taskId, report, supersede);
    public Task<CliHttpResult> RecordAgentEvidenceAsync(Guid taskId, string kind, string summary, object? detail = null) =>
        inner.RecordAgentEvidenceAsync(taskId, kind, summary, detail);
}
