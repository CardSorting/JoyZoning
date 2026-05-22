using System.Net;
using System.Text.Json;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;

namespace JoyZoning.App.Services;

public sealed record ApiErrorResponse(string Error, string Message, string? NextAction = null);

public sealed class ApiCallResult<T>
{
    public bool Success { get; init; }
    public HttpStatusCode StatusCode { get; init; }
    public T? Data { get; init; }
    public ApiErrorResponse? Error { get; init; }

    public static ApiCallResult<T> Ok(T data, HttpStatusCode status = HttpStatusCode.OK) =>
        new() { Success = true, StatusCode = status, Data = data };

    public static ApiCallResult<T> Fail(HttpStatusCode status, ApiErrorResponse error) =>
        new() { Success = false, StatusCode = status, Error = error };
}

public static class OperatorApiHints
{
    public static string? NextActionFor(string? errorCode, string? message)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
            return null;

        return errorCode switch
        {
            "lease_forbidden" when message?.Contains("humanApprovedCritical", StringComparison.OrdinalIgnoreCase) == true
                => "Check “Approve critical dispatch” and try again.",
            "lease_forbidden" when message?.Contains("human", StringComparison.OrdinalIgnoreCase) == true
                => "Use Merge on the kanban card after verification passes.",
            "lease_conflict" when message?.Contains("critical", StringComparison.OrdinalIgnoreCase) == true
                => "Wait for the other critical card to finish or revoke its lease.",
            "lease_conflict" when message?.Contains("active", StringComparison.OrdinalIgnoreCase) == true
                => "Revoke the current lease or wait until it is merged/revoked.",
            "lease_conflict" when message?.Contains("Dispatch failed", StringComparison.OrdinalIgnoreCase) == true
                => "Review the blocked lease evidence, fix Hermes connectivity, then revoke and dispatch again.",
            "lease_conflict" when message?.Contains("supersede", StringComparison.OrdinalIgnoreCase) == true
                => "Set supersede=true only when intentionally replacing a passing report.",
            "lease_invalid_request" when message?.Contains("Reason", StringComparison.OrdinalIgnoreCase) == true
                => "Provide a reason when moving the lease to Blocked.",
            "lease_invalid_request" when message?.Contains("commandsRun", StringComparison.OrdinalIgnoreCase) == true
                => "Include at least one command with pass/fail and a summary.",
            "lease_not_found" => "Dispatch the card first to create an execution lease.",
            "task_not_found" => "Refresh the board and select a valid card.",
            _ => null,
        };
    }

    public static string FormatError(ApiErrorResponse error) =>
        error.NextAction is { Length: > 0 } next
            ? $"{error.Message} — {next}"
            : error.Message;
}

public sealed record LeaseInfo(
    Guid Id,
    Guid WorkTaskId,
    ExecutionLeaseStatus Status,
    string WorktreePath,
    string BranchName,
    bool CriticalApprovalGranted,
    bool CriticalApprovalConsumed,
    string? BlockedReason);

public sealed record DispatchOptions(bool HumanApprovedCritical = false);

public sealed record AgentLeaseStatusRequest(
    ExecutionLeaseStatus Status,
    string? Reason);

public sealed record SubmitVerificationRequest(
    VerificationReport Report,
    bool Supersede = false);

public sealed record RevokeLeaseRequest(string? Reason);
