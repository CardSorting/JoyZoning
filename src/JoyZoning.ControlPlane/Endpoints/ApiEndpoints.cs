using JoyZoning.Adapters.Workspace;
using JoyZoning.Agents.Hermes;
using JoyZoning.ControlPlane.Background;
using JoyZoning.ControlPlane.Services;
using AppSettingsDto = JoyZoning.ControlPlane.Services.AppSettingsDto;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using JoyZoning.Persistence.Repositories;

namespace JoyZoning.ControlPlane.Endpoints;

public static class ApiEndpoints
{
    public static void MapJoyZoningApi(this WebApplication app)
    {
        app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "joyzoning-control-plane" }));

        app.MapGet("/api/sessions", async (IOperatorSessionRepository repo) =>
            Results.Ok(await repo.ListAsync()));

        app.MapPost("/api/sessions", async (CreateSessionRequest req, OrchestrationService orch) =>
        {
            var session = await orch.CreateSessionAsync(req.Name, req.WorkspaceRoot, req.HermesProfile);
            return Results.Created($"/api/sessions/{session.Id}", session);
        });

        app.MapGet("/api/sessions/{id:guid}", async (Guid id, IOperatorSessionRepository repo) =>
        {
            var session = await repo.GetByIdAsync(id);
            return session is null ? Results.NotFound() : Results.Ok(session);
        });

        app.MapGet("/api/tasks", async (Guid? sessionId, IWorkTaskRepository repo) =>
        {
            if (!sessionId.HasValue) return Results.BadRequest("sessionId required");
            return Results.Ok(await repo.ListBySessionAsync(sessionId.Value));
        });

        app.MapPost("/api/tasks", async (CreateTaskRequest req, OrchestrationService orch) =>
        {
            var task = await orch.CreateTaskAsync(
                req.SessionId, req.Title, req.Description ?? "",
                req.AssignedAgent, req.Risk);
            return Results.Created($"/api/tasks/{task.Id}", task);
        });

        app.MapPut("/api/tasks/{id:guid}/status", async (
            Guid id,
            UpdateTaskStatusRequest req,
            OrchestrationService orch) =>
        {
            try
            {
                return Results.Ok(await orch.UpdateTaskStatusAsync(id, req.Status, req.Actor));
            }
            catch (Exception ex)
            {
                return LeaseApiResults.FromException(ex);
            }
        });

        app.MapGet("/api/tasks/{id:guid}/lease", async (
            Guid id,
            KanbanExecutionOrchestrator exec) =>
        {
            try
            {
                var lease = await exec.GetActiveLeaseAsync(id);
                return lease is null
                    ? Results.Json(new { error = "lease_not_found", message = "No active lease." }, statusCode: 404)
                    : Results.Ok(lease);
            }
            catch (Exception ex)
            {
                return LeaseApiResults.FromException(ex);
            }
        });

        app.MapPost("/api/tasks/{id:guid}/lease/agent-evidence", async (
            Guid id,
            AgentEvidenceRequest req,
            KanbanExecutionOrchestrator exec) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Kind))
                    return Results.Json(
                        new { error = "lease_invalid_request", message = "kind is required." },
                        statusCode: 400);

                return Results.Ok(await exec.RecordAgentEvidenceAsync(
                    id, req.Kind.Trim(), req.Summary ?? req.Kind, req.Detail));
            }
            catch (Exception ex)
            {
                return LeaseApiResults.FromException(ex);
            }
        });

        app.MapPost("/api/tasks/{id:guid}/lease/heartbeat", async (
            Guid id,
            LeaseHeartbeatRequest req,
            IWorkTaskRepository tasks,
            KanbanExecutionOrchestrator exec) =>
        {
            try
            {
                var task = await tasks.GetByIdAsync(id);
                if (task is null)
                    return Results.Json(new { error = "task_not_found", message = "Task not found." }, statusCode: 404);

                var ctx = new LeaseOperationContext(req.SessionId, req.Actor);
                return Results.Ok(await exec.RecordHeartbeatAsync(id, ctx));
            }
            catch (Exception ex)
            {
                return LeaseApiResults.FromException(ex);
            }
        });

        app.MapPost("/api/tasks/{id:guid}/lease/recover", async (
            Guid id,
            RecoverLeaseRequest req,
            KanbanExecutionOrchestrator exec) =>
        {
            try
            {
                var ctx = new LeaseOperationContext(req.SessionId, req.Actor, RecoveryFlow: true);
                return Results.Ok(await exec.RecoverLeaseAsync(
                    id, req.Mode, ctx, req.HumanApprovedCritical));
            }
            catch (Exception ex)
            {
                return LeaseApiResults.FromException(ex);
            }
        });

        app.MapPost("/api/tasks/{id:guid}/lease/fail", async (
            Guid id,
            FailLeaseRequest req,
            KanbanExecutionOrchestrator exec) =>
        {
            try
            {
                var lease = await exec.GetActiveLeaseAsync(id)
                    ?? throw LeaseOrchestrationException.NotFound("No active lease.");

                var reason = string.IsNullOrWhiteSpace(req.Reason)
                    ? "Operator recorded failure from CLI."
                    : req.Reason.Trim();

                return lease.Status switch
                {
                    ExecutionLeaseStatus.Leased =>
                        Results.Ok(await exec.RecordDispatchFailureAsync(id, reason)),
                    ExecutionLeaseStatus.Running =>
                        Results.Ok(await exec.RecordExecutionFailureAsync(id, reason)),
                    ExecutionLeaseStatus.Verifying =>
                        Results.Ok(await exec.AgentTransitionLeaseAsync(
                            id,
                            ExecutionLeaseStatus.Blocked,
                            reason,
                            new LeaseOperationContext(lease.AssignedSessionId, StatusChangeActor.DietCode))),
                    _ => Results.Json(
                        new
                        {
                            error = "lease_conflict",
                            message = $"Cannot record failure from lease status {lease.Status}.",
                        },
                        statusCode: 409),
                };
            }
            catch (Exception ex)
            {
                return LeaseApiResults.FromException(ex);
            }
        });

        app.MapPost("/api/tasks/{id:guid}/lease/dispatch-retry", async (
            Guid id,
            DispatchRetryRequest req,
            KanbanExecutionOrchestrator exec) =>
        {
            try
            {
                var ctx = new LeaseOperationContext(req.SessionId, req.Actor);
                return Results.Ok(await exec.RecordDispatchRetryAsync(
                    id, req.HumanApprovedCritical, ctx));
            }
            catch (Exception ex)
            {
                return LeaseApiResults.FromException(ex);
            }
        });

        app.MapPost("/api/tasks/{id:guid}/lease/agent-status", async (
            Guid id,
            AgentLeaseStatusRequest req,
            IWorkTaskRepository tasks,
            KanbanExecutionOrchestrator exec) =>
        {
            try
            {
                if (!KanbanExecutionRules.AgentAllowedLeaseTargets.Contains(req.Status))
                    return Results.Json(
                        new { error = "lease_invalid_request", message = "Invalid agent lease status." },
                        statusCode: 400);

                if (req.Status == ExecutionLeaseStatus.Blocked && string.IsNullOrWhiteSpace(req.Reason))
                    return Results.Json(
                        new { error = "lease_invalid_request", message = "Reason is required when blocking." },
                        statusCode: 400);

                var task = await tasks.GetByIdAsync(id);
                if (task is null)
                    return Results.Json(new { error = "task_not_found", message = "Task not found." }, statusCode: 404);

                var ctx = new LeaseOperationContext(task.OperatorSessionId, StatusChangeActor.DietCode);
                return Results.Ok(await exec.AgentTransitionLeaseAsync(id, req.Status, req.Reason, ctx));
            }
            catch (Exception ex)
            {
                return LeaseApiResults.FromException(ex);
            }
        });

        app.MapPost("/api/tasks/{id:guid}/verification", async (
            Guid id,
            SubmitVerificationRequest req,
            KanbanExecutionOrchestrator exec) =>
        {
            try
            {
                if (req.Report.CommandsRun.Count == 0)
                    return Results.Json(
                        new { error = "lease_invalid_request", message = "commandsRun is required." },
                        statusCode: 400);

                if (req.Report.AllCommandsPassed)
                    return Results.Ok(await exec.SubmitVerificationAsync(id, req.Report, req.Supersede));

                return Results.Ok(await exec.SubmitFailedVerificationAsync(id, req.Report));
            }
            catch (Exception ex)
            {
                return LeaseApiResults.FromException(ex);
            }
        });

        app.MapPost("/api/tasks/{id:guid}/lease/revoke", async (
            Guid id,
            RevokeLeaseRequest? req,
            KanbanExecutionOrchestrator exec) =>
        {
            try
            {
                return Results.Ok(await exec.RevokeLeaseAsync(id, req?.Reason));
            }
            catch (Exception ex)
            {
                return LeaseApiResults.FromException(ex);
            }
        });

        app.MapPost("/api/tasks/{id:guid}/lease/merge", async (
            Guid id,
            KanbanExecutionOrchestrator exec) =>
        {
            try
            {
                return Results.Ok(await exec.ApproveMergeAsync(id));
            }
            catch (Exception ex)
            {
                return LeaseApiResults.FromException(ex);
            }
        });

        app.MapPost("/api/tasks/import-kanban", async (ImportKanbanRequest req, OrchestrationService orch) =>
        {
            var result = await orch.ImportKanbanTasksAsync(req.SessionId);
            return Results.Ok(result);
        });

        app.MapPost("/api/tasks/{id:guid}/dispatch", async (
            Guid id,
            DispatchTaskRequest? req,
            IWorkTaskRepository tasks,
            OrchestrationService orch,
            HermesRunEventConsumer consumer) =>
        {
            try
            {
                var task = await tasks.GetByIdAsync(id);
                if (task is null)
                    return Results.Json(
                        new { error = "task_not_found", message = "Task not found." },
                        statusCode: 404);

                var wantsCritical = req?.HumanApprovedCritical == true;
                if (task.Risk >= RiskLevel.Critical && !wantsCritical)
                    return Results.Json(
                        new
                        {
                            error = "lease_forbidden",
                            message = "Critical card requires humanApprovedCritical=true on dispatch.",
                        },
                        statusCode: 403);

                var execution = await orch.DispatchTaskAsync(id, wantsCritical);
                consumer.TrackRun(execution.HermesRunId, AgentKind.DietCode, id);
                return Results.Accepted($"/api/executions/{execution.Id}", execution);
            }
            catch (Exception ex)
            {
                return LeaseApiResults.FromException(ex);
            }
        });

        app.MapPost("/api/manager/message", async (
            ManagerMessageRequest req,
            OrchestrationService orch,
            HermesRunEventConsumer consumer) =>
        {
            var runId = await orch.SendManagerMessageAsync(req.SessionId, req.Message);
            consumer.TrackRun(runId, AgentKind.Hermes, req.SessionId);
            return Results.Json(new { runId });
        });

        app.MapGet("/api/executions/{id:guid}", async (Guid id, IExecutionRepository repo) =>
        {
            var ex = await repo.GetByIdAsync(id);
            return ex is null ? Results.NotFound() : Results.Ok(ex);
        });

        app.MapGet("/api/approvals/pending", async (ApprovalService svc) =>
            Results.Ok(await svc.ListPendingAsync()));

        app.MapPost("/api/approvals/{id:guid}/resolve", async (
            Guid id,
            ResolveApprovalRequest req,
            ApprovalService svc) =>
            Results.Ok(await svc.ResolveAsync(id, req.Scope)));

        app.MapGet("/api/events", async (
            long? since,
            Guid? correlationId,
            string? types,
            IEventRepository repo) =>
        {
            var typeList = string.IsNullOrEmpty(types)
                ? null
                : types.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var events = await repo.QueryAsync(since, correlationId, typeList);
            return Results.Ok(events);
        });

        app.MapGet("/api/workspace/tree", async (string workspaceRoot, IWorkspaceAdapter workspace) =>
            Results.Ok(await workspace.ListTreeAsync(workspaceRoot)));

        app.MapGet("/api/workspace/changed", async (string workspaceRoot, IWorkspaceAdapter workspace) =>
            Results.Ok(await workspace.ListChangedFilesAsync(workspaceRoot)));

        app.MapGet("/api/workspace/diff", async (string workspaceRoot, string path, IWorkspaceAdapter workspace) =>
        {
            var diff = await workspace.GetGitDiffAsync(workspaceRoot, path);
            return diff is null
                ? Results.NotFound(new { message = "Not a git repo or no diff for path." })
                : Results.Ok(new { path, diff });
        });

        app.MapGet("/api/hermes/health", async (
            HermesConnectivityService connectivity,
            HermesRuntimeSettings runtime) =>
        {
            var report = await connectivity.GetHealthAsync();
            return Results.Ok(new
            {
                state = report.State.ToString(),
                message = report.Message,
                apiUrl = runtime.ApiBaseUrl,
            });
        });

        app.MapPost("/api/hermes/ensure", async (HermesConnectivityService connectivity) =>
        {
            var report = await connectivity.EnsureAsync();
            return Results.Ok(new
            {
                status = report.State == HealthState.Healthy ? "healthy" : "unavailable",
                state = report.State.ToString(),
                message = report.Message,
            });
        });

        app.MapGet("/api/hermes/dashboard", async (HermesDashboardConnectivityService dashboard) =>
        {
            var status = await dashboard.GetStatusAsync();
            return Results.Ok(new
            {
                reachable = status.Reachable,
                hasToken = status.HasToken,
                tokenValid = status.TokenValid,
                hermesCliFound = status.HermesCliFound,
                dashboardUrl = status.DashboardUrl,
                message = status.Message,
                ready = status.Reachable && status.TokenValid,
            });
        });

        app.MapPost("/api/hermes/ensure-dashboard", async (
            HermesDashboardConnectivityService dashboard,
            EnsureDashboardRequest? req) =>
        {
            var result = await dashboard.EnsureAsync(req?.AlsoEnsureGateway ?? true);
            return Results.Ok(new
            {
                reachable = result.Reachable,
                tokenAcquired = result.TokenAcquired,
                tokenValid = result.TokenValid,
                ready = result.Ready,
                message = result.Message,
                steps = result.Steps.Select(s => new { s.Id, s.Label, status = s.Status, detail = s.Detail }),
            });
        });

        app.MapPost("/api/hermes/refresh-dashboard-token", async (HermesDashboardConnectivityService dashboard) =>
        {
            var result = await dashboard.RefreshTokenAsync();
            return Results.Ok(new
            {
                reachable = result.Reachable,
                tokenAcquired = result.TokenAcquired,
                tokenValid = result.TokenValid,
                ready = result.Ready,
                message = result.Message,
                steps = result.Steps.Select(s => new { s.Id, s.Label, status = s.Status, detail = s.Detail }),
            });
        });

        app.MapGet("/api/config", async (ConfigService config) =>
            Results.Ok(await config.GetSettingsAsync()));

        app.MapPut("/api/config", async (AppSettingsDto settings, ConfigService config) =>
        {
            await config.SaveSettingsAsync(settings);
            return Results.Ok(await config.GetSettingsAsync());
        });

        app.MapGet("/api/kanban/sync-status", (KanbanSyncState state) =>
            Results.Ok(new
            {
                lastSyncAt = state.LastSyncAt,
                message = state.LastMessage,
            }));

        app.MapGet("/api/executions/interrupted", async (IExecutionRepository repo) =>
        {
            var list = await repo.ListInterruptedAsync();
            return Results.Ok(list);
        });

        app.MapPost("/api/executions/{id:guid}/resume", async (
            Guid id,
            OrchestrationService orch,
            HermesRunEventConsumer consumer) =>
        {
            var execution = await orch.ResumeExecutionAsync(id);
            if (execution is null) return Results.NotFound();
            consumer.TrackRun(execution.HermesRunId, AgentKind.DietCode, execution.WorkTaskId);
            return Results.Ok(execution);
        });

        app.MapPost("/api/executions/{id:guid}/cancel", async (
            Guid id,
            OrchestrationService orch) =>
        {
            await orch.MarkExecutionCancelledAsync(id);
            return Results.Ok();
        });
    }
}

public record ImportKanbanRequest(Guid SessionId);
public record EnsureDashboardRequest(bool AlsoEnsureGateway = true);

public record CreateSessionRequest(string Name, string WorkspaceRoot, string? HermesProfile);
public record CreateTaskRequest(
    Guid SessionId,
    string Title,
    string? Description,
    AgentKind AssignedAgent = AgentKind.DietCode,
    RiskLevel Risk = RiskLevel.Low);
public record UpdateTaskStatusRequest(
    WorkTaskStatus Status,
    StatusChangeActor Actor = StatusChangeActor.Human);
public record DispatchTaskRequest(bool HumanApprovedCritical = false);
public record AgentLeaseStatusRequest(ExecutionLeaseStatus Status, string? Reason);
public record SubmitVerificationRequest(VerificationReport Report, bool Supersede = false);
public record RevokeLeaseRequest(string? Reason);
public record LeaseHeartbeatRequest(Guid SessionId, StatusChangeActor Actor = StatusChangeActor.DietCode);
public record RecoverLeaseRequest(
    Guid SessionId,
    LeaseRecoveryMode Mode,
    StatusChangeActor Actor = StatusChangeActor.Human,
    bool HumanApprovedCritical = false);
public record DispatchRetryRequest(
    Guid SessionId,
    StatusChangeActor Actor = StatusChangeActor.Human,
    bool HumanApprovedCritical = false);
public record FailLeaseRequest(string? Reason);
public record AgentEvidenceRequest(string Kind, string? Summary, object? Detail);
public record ManagerMessageRequest(Guid SessionId, string Message);
public record ResolveApprovalRequest(ApprovalScope Scope);
