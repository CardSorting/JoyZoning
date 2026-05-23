using JoyZoning.Agents;
using JoyZoning.Agents.Approval;
using JoyZoning.Domain.Agents;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Events;
using JoyZoning.Persistence.Repositories;
using Microsoft.AspNetCore.SignalR;
using JoyZoning.ControlPlane.Hubs;

namespace JoyZoning.ControlPlane.Services;

public class ApprovalService
{
    private readonly IApprovalRepository _approvals;
    private readonly IApprovalGrantRepository _grants;
    private readonly IExecutionRepository _executions;
    private readonly AgentAdapterRegistry _agents;
    private readonly EventIngestor _events;
    private readonly IHubContext<OperatorHub> _hub;

    public ApprovalService(
        IApprovalRepository approvals,
        IApprovalGrantRepository grants,
        IExecutionRepository executions,
        AgentAdapterRegistry agents,
        EventIngestor events,
        IHubContext<OperatorHub> hub)
    {
        _approvals = approvals;
        _grants = grants;
        _executions = executions;
        _agents = agents;
        _events = events;
        _hub = hub;
    }

    public async Task<ApprovalRequest?> CreateFromHermesEventAsync(
        string runId,
        string command,
        string description,
        AgentKind agent,
        Guid? taskId,
        CancellationToken cancellationToken = default)
    {
        var (category, risk) = RiskClassifier.Classify(command, description);
        var execution = await _executions.GetByRunIdAsync(runId, cancellationToken);
        var workTaskId = taskId ?? execution?.WorkTaskId;

        if (workTaskId.HasValue &&
            await _grants.HasActiveGrantAsync(workTaskId.Value, category, cancellationToken))
        {
            var agentAdapter = _agents.Get(agent);
            try
            {
                await agentAdapter.ResolveApprovalAsync(
                    runId, new ApprovalResolution(ApprovalScope.Once), cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Auto-approve failed for run {runId}: {ex.Message}", ex);
            }

            await _events.IngestAsync(
                workTaskId.Value,
                EventSource.JoyZoning,
                EventTypes.ApprovalGranted,
                new { runId, auto = true, category = category.ToString() },
                cancellationToken);
            return null;
        }

        var request = new ApprovalRequest
        {
            Id = Guid.NewGuid(),
            HermesRunId = runId,
            WorkTaskId = workTaskId,
            ExecutionSessionId = execution?.Id,
            RequestingAgent = agent,
            Category = category,
            Risk = risk,
            Command = command,
            Description = description,
            Status = ApprovalStatus.Pending,
            RequestedAt = DateTimeOffset.UtcNow,
        };

        await _approvals.CreateAsync(request, cancellationToken);

        await _events.IngestAsync(
            workTaskId ?? request.Id,
            EventSource.Hermes,
            EventTypes.HermesApprovalRequested,
            new { request.Id, runId, command, risk = risk.ToString() },
            cancellationToken);

        await _hub.Clients.All.SendAsync(
            "OnApprovalRequested",
            ApprovalDto.From(request),
            cancellationToken);

        return request;
    }

    public async Task<ApprovalRequest> ResolveAsync(
        Guid approvalId,
        ApprovalScope scope,
        CancellationToken cancellationToken = default)
    {
        var request = await _approvals.GetByIdAsync(approvalId, cancellationToken)
            ?? throw new InvalidOperationException($"Approval {approvalId} not found");

        var status = scope == ApprovalScope.Deny ? ApprovalStatus.Denied : ApprovalStatus.Approved;

        if (!string.IsNullOrEmpty(request.HermesRunId))
        {
            var agent = _agents.Get(request.RequestingAgent);
            var hermesScope = scope == ApprovalScope.Task ? ApprovalScope.Once : scope;
            try
            {
                await agent.ResolveApprovalAsync(
                    request.HermesRunId,
                    new ApprovalResolution(hermesScope),
                    cancellationToken);
            }
            catch (Exception ex) when (status == ApprovalStatus.Approved)
            {
                throw new InvalidOperationException(
                    $"Hermes rejected approval resolution for run {request.HermesRunId}: {ex.Message}", ex);
            }
        }

        await _approvals.ResolveAsync(approvalId, status, scope, cancellationToken);

        if (status == ApprovalStatus.Approved &&
            scope == ApprovalScope.Task &&
            request.WorkTaskId.HasValue)
        {
            await _grants.CreateAsync(new ApprovalGrant
            {
                Id = Guid.NewGuid(),
                WorkTaskId = request.WorkTaskId.Value,
                Category = request.Category,
                Scope = ApprovalScope.Task,
                GrantedAt = DateTimeOffset.UtcNow,
            }, cancellationToken);
        }

        var eventType = status == ApprovalStatus.Approved
            ? EventTypes.ApprovalGranted
            : EventTypes.ApprovalDenied;

        await _events.IngestAsync(
            request.WorkTaskId ?? request.Id,
            EventSource.JoyZoning,
            eventType,
            new { approvalId, scope = scope.ToString() },
            cancellationToken);

        request.Status = status;
        request.GrantedScope = scope;
        request.ResolvedAt = DateTimeOffset.UtcNow;
        return request;
    }

    public Task<IReadOnlyList<ApprovalRequest>> ListPendingAsync(CancellationToken cancellationToken = default) =>
        _approvals.ListPendingAsync(cancellationToken);
}

public record ApprovalDto(
    Guid Id,
    Guid? WorkTaskId,
    string? HermesRunId,
    string RequestingAgent,
    string Category,
    string Risk,
    string Command,
    string Description,
    string Status)
{
    public static ApprovalDto From(ApprovalRequest r) => new(
        r.Id, r.WorkTaskId, r.HermesRunId, r.RequestingAgent.ToString(),
        r.Category.ToString(), r.Risk.ToString(), r.Command, r.Description, r.Status.ToString());
}
