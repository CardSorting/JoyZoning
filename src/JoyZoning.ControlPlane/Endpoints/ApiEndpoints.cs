using JoyZoning.Adapters.Workspace;
using JoyZoning.Agents.Hermes;
using JoyZoning.ControlPlane.Background;
using JoyZoning.ControlPlane.Services;
using AppSettingsDto = JoyZoning.ControlPlane.Services.AppSettingsDto;
using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using Microsoft.Extensions.Options;
using JoyZoning.Domain.Orchestration;
using JoyZoning.Persistence.Repositories;

namespace JoyZoning.ControlPlane.Endpoints;

public static class ApiEndpoints
{
    public static void MapJoyZoningApi(this WebApplication app)
    {
        app.MapGet("/api/health", async (
            IBroccoliQBridge bridge,
            BroccoliQRuntimeMetrics metrics) =>
        {
            object? broccoliq = null;
            if (bridge.IsEnabled)
            {
                var report = await bridge.GetHealthAsync();
                var mirror = metrics.Snapshot();
                broccoliq = new
                {
                    status = report.State == HealthState.Healthy ? "ok" : "unavailable",
                    enabled = true,
                    bridgeReady = report.State == HealthState.Healthy,
                    queueDepth = mirror.QueueDepth,
                    dropped = mirror.Dropped,
                };
            }

            return Results.Ok(new
            {
                status = "ok",
                service = "joyzoning-control-plane",
                broccoliq,
            });
        });

        app.MapGet("/api/broccoliq/health", async (
            IBroccoliQBridge bridge,
            BroccoliQProcessService process,
            BroccoliQRuntimeMetrics metrics,
            BroccoliQCoordinator coordinator,
            IOptions<BroccoliQOptions> bqOpts) =>
        {
            var report = await bridge.GetHealthAsync();
            var mirror = metrics.Snapshot();
            var status = report.State == HealthState.Healthy ? "ok" : "unavailable";
            return Results.Ok(new
            {
                status,
                enabled = report.Enabled,
                workerAutoStart = report.WorkerAutoStart,
                supervisorEnabled = bqOpts.Value.SupervisorEnabled,
                backfillOnStartup = bqOpts.Value.BackfillOnStartup,
                message = report.Message,
                databasePath = report.DatabasePath,
                bridgeBuilt = report.BridgeBuilt,
                bridgeReady = coordinator.BridgeReady,
                bridgeMirrorCount = report.BridgeMirrorCount,
                lastBackfillEventId = coordinator.LastBackfillEventId,
                workerManaged = process.IsManagedWorkerRunning,
                mirror,
            });
        });

        app.MapPost("/api/broccoliq/backfill", async (
            BroccoliQBackfillService backfill,
            int? maxEvents,
            bool? includeTasks) =>
        {
            var result = await backfill.RunAsync(maxEvents, includeTasks ?? true);
            return Results.Ok(result);
        });

        app.MapPost("/api/broccoliq/flush", async (IBroccoliQBridge bridge) =>
        {
            await bridge.FlushBridgeAsync();
            return Results.Ok(new { flushed = true });
        });

        app.MapGet("/api/broccoliq/audit", async (
            IBroccoliQBridge bridge,
            int? limit,
            string? typePrefix) =>
        {
            var page = await bridge.QueryHiveAuditAsync(
                limit ?? 100,
                typePrefix ?? "joy.");
            return Results.Ok(page);
        });

        app.MapGet("/api/broccoliq/tasks", async (
            IBroccoliQBridge bridge,
            int? limit,
            Guid? taskId) =>
        {
            var page = await bridge.QueryHiveTasksAsync(limit ?? 100, taskId);
            return Results.Ok(page);
        });

        app.MapGet("/api/sessions", async (IOperatorSessionRepository repo) =>
        {
            var all = await repo.ListAsync();
            return Results.Ok(WorkspaceSessionCatalog.SelectCanonicalSessions(all));
        });

        app.MapPost("/api/sessions", async (CreateSessionRequest req, OrchestrationService orch) =>
        {
            var session = await orch.CreateSessionAsync(req.Name, req.WorkspaceRoot, req.HermesProfile);
            return Results.Created($"/api/sessions/{session.Id}", session);
        });

        app.MapPost("/api/sessions/consolidate", async (WorkspaceSessionConsolidator consolidator) =>
        {
            await consolidator.ConsolidateAllAsync();
            return Results.Ok(new { consolidated = true });
        });

        app.MapGet("/api/sessions/{id:guid}", async (Guid id, OrchestrationService orch) =>
        {
            try
            {
                var session = await orch.ResolveCanonicalSessionAsync(id);
                return Results.Ok(session);
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound();
            }
        });

        app.MapGet("/api/tasks", async (
            Guid? sessionId,
            IWorkTaskRepository tasks,
            OrchestrationService orch) =>
        {
            if (!sessionId.HasValue)
                return Results.BadRequest("sessionId required");

            try
            {
                var session = await orch.ResolveCanonicalSessionAsync(sessionId.Value);
                if (session.IsBoundedRoleSession)
                    return Results.Ok(await tasks.ListBySessionAsync(session.Id));

                return Results.Ok(await tasks.ListByWorkspaceRootAsync(session.WorkspaceRoot));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound();
            }
        });

        app.MapGet("/api/tasks/{id:guid}", async (Guid id, IWorkTaskRepository tasks) =>
        {
            var task = await tasks.GetByIdAsync(id);
            return task is null ? Results.NotFound() : Results.Ok(task);
        });

        app.MapGet("/api/watch/bootstrap", async (
            IOperatorSessionRepository sessions,
            IWorkTaskRepository tasks,
            IExecutionLeaseRepository leases,
            CancellationToken cancellationToken) =>
        {
            var sessionList = WorkspaceSessionCatalog.SelectCanonicalSessions(
                await sessions.ListAsync(cancellationToken));
            var activeLeases = await leases.ListActiveAsync(cancellationToken);
            var activeTasks = new List<object>();

            foreach (var lease in activeLeases)
            {
                var task = await tasks.GetByIdAsync(lease.WorkTaskId, cancellationToken);
                activeTasks.Add(new
                {
                    taskId = lease.WorkTaskId,
                    sessionId = lease.OperatorSessionId,
                    title = task?.Title ?? "Task",
                    leaseStatus = lease.Status.ToString(),
                    blockedReason = lease.BlockedReason,
                    worktreePath = lease.WorktreePath,
                });
            }

            return Results.Ok(new
            {
                watchUrl = "/",
                agentOps = new
                {
                    manifestVersion = AgentOperationsManifest.ManifestVersion,
                    fingerprint = AgentOperationsManifestCache.ComputeStaticFingerprint(),
                    manifestUrl = "/api/agent/manifest",
                    contextUrl = "/api/agent/context",
                    endpointsUrl = "/api/agent/endpoints?agentSafe=true",
                    contractPath = AgentOperationsManifest.AgentContractRelativePath,
                    agentsEntry = "AGENTS.md",
                    manifestCache = AgentOperationsManifestCache.RelativePath,
                },
                sessions = sessionList.Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.WorkspaceRoot,
                    workspaceKey = s.WorkspaceKey ?? s.WorkspaceRoot,
                    s.HermesProfile,
                }),
                activeTasks,
            });
        });

        app.MapPost("/api/sessions/open-path", (OpenWorkspacePathRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.Path) || !Directory.Exists(req.Path))
                return Results.BadRequest(new { message = "Path does not exist." });

            var full = Path.GetFullPath(req.Path);
            try
            {
                if (OperatingSystem.IsMacOS())
                {
                    System.Diagnostics.Process.Start("open", full);
                    return Results.Ok(new { ok = true });
                }

                if (OperatingSystem.IsLinux())
                {
                    System.Diagnostics.Process.Start("xdg-open", full);
                    return Results.Ok(new { ok = true });
                }

                return Results.StatusCode(StatusCodes.Status501NotImplemented);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message, statusCode: 500);
            }
        });

        app.MapGet("/api/sessions/{id:guid}/parallel-workers", async (
            Guid id,
            WorkspaceWorkerObservabilityService observability,
            OrchestrationService orch,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var canonical = await orch.ResolveCanonicalSessionAsync(id, cancellationToken);
                var model = await observability.GetParallelWorkersAsync(canonical.Id, cancellationToken);
                return Results.Ok(ParallelWorkersApiMapper.ToJson(model));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound();
            }
        });

        app.MapGet("/api/sessions/{id:guid}/merge-queue", async (
            Guid id,
            WorkspaceWorkerObservabilityService observability,
            OrchestrationService orch,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var canonical = await orch.ResolveCanonicalSessionAsync(id, cancellationToken);
                var model = await observability.GetMergeQueueAsync(canonical.Id, cancellationToken);
                return Results.Ok(MergeQueueApiMapper.ToJson(model));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound();
            }
        });

        app.MapGet("/api/sessions/{id:guid}/delivery-plan", async (
            Guid id,
            IWorkTaskRepository tasks,
            IOperatorSessionRepository sessions,
            IExecutionLeaseRepository leases,
            IOptions<LeaseRuntimeOptions> leaseOptions,
            OrchestrationService orch,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var canonical = await orch.ResolveCanonicalSessionAsync(id, cancellationToken);
                var session = await sessions.GetByIdAsync(canonical.Id, cancellationToken)
                    ?? throw new InvalidOperationException("Session not found.");
                var sessionTasks = await tasks.ListBySessionAsync(session.Id, cancellationToken);
                var activeLeases = (await leases.ListActiveAsync(cancellationToken))
                    .Where(l => l.OperatorSessionId == session.Id)
                    .ToList();
                var plan = BoundedSessionGate.BuildPlan(
                    session,
                    sessionTasks,
                    activeLeases,
                    leaseOptions.Value);

                string? chainBlockReason = null;
                object? jsdp = null;
                if (session.IsBoundedRoleSession && session.DeliveryChainId.HasValue)
                {
                    var chainSessions = await sessions.ListByDeliveryChainIdAsync(session.DeliveryChainId.Value, cancellationToken);
                    var chainTasks = new List<WorkTask>();
                    foreach (var chainSession in chainSessions)
                        chainTasks.AddRange(await tasks.ListBySessionAsync(chainSession.Id, cancellationToken));

                    var chainLeases = new List<ExecutionLease>();
                    foreach (var chainTask in chainTasks)
                        chainLeases.AddRange(await leases.ListByTaskIdAsync(chainTask.Id, cancellationToken));

                    var queue = RoleDeliveryChainGate.BuildQueue(
                        session.DeliveryChainId.Value,
                        session.WorkspaceRoot,
                        chainSessions,
                        chainTasks,
                        chainLeases,
                        leaseOptions.Value);
                    chainBlockReason = queue.BlockReason;
                    jsdp = new
                    {
                        protocol = queue.Jsdp.Protocol,
                        mergeGateStatus = queue.Jsdp.MergeGateStatus,
                        nextDispatchEligibility = queue.Jsdp.NextDispatchEligibility,
                        nextHumanAction = queue.Jsdp.NextHumanAction,
                        blockReason = chainBlockReason,
                    };
                }

                return Results.Ok(new
                {
                    model = plan.Model,
                    sessionId = plan.SessionId,
                    sessionWorkspaceRoot = plan.SessionWorkspaceRoot,
                    executionMode = session.ExecutionMode.ToString(),
                    deliveryChainId = session.DeliveryChainId,
                    deliverySequence = session.DeliverySequence,
                    maxActiveLeasesPerSession = plan.MaxActiveLeasesPerSession,
                    activeLeaseCount = plan.ActiveLeaseCount,
                    inFlightTaskId = plan.InFlightTaskId,
                    inFlightTaskTitle = plan.InFlightTaskTitle,
                    inFlightTaskStatus = plan.InFlightTaskStatus?.ToString(),
                    hasFoundation = plan.HasFoundation,
                    nextDispatchableTaskId = chainBlockReason is null ? plan.NextDispatchableTaskId : null,
                    chainBlockReason,
                    jsdp,
                    tasks = plan.Tasks.Select(t => new
                    {
                        taskId = t.TaskId,
                        title = t.Title,
                        role = DeliveryRoleClassifier.RoleLabel(t.Role),
                        status = t.Status.ToString(),
                        dispatchable = chainBlockReason is null && t.Dispatchable,
                        blockReason = chainBlockReason ?? t.BlockReason,
                    }),
                });
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound();
            }
        });

        app.MapPost("/api/delivery-chains", async (
            CreateDeliveryChainRequest req,
            OrchestrationService orch) =>
        {
            try
            {
                var roles = req.Roles is { Count: > 0 }
                    ? req.Roles.Select(r => new RoleDeliveryChainRoleSpec(
                        r.Title,
                        r.Description ?? r.Title,
                        r.AssignedAgent,
                        r.Risk)).ToList()
                    : RoleDeliveryChainTemplates.DefaultEightRoles;

                var result = await orch.CreateRoleDeliveryChainAsync(
                    req.ProgramName,
                    req.WorkspaceRoot,
                    roles,
                    req.HermesProfile);

                return Results.Created(
                    $"/api/delivery-chains/{result.ChainId}/queue",
                    result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = "delivery_chain_invalid", message = ex.Message });
            }
        });

        app.MapGet("/api/delivery-chains/{id:guid}/queue", async (
            Guid id,
            IOperatorSessionRepository sessions,
            IWorkTaskRepository tasks,
            IExecutionLeaseRepository leases,
            IOptions<LeaseRuntimeOptions> leaseOptions) =>
        {
            var chainSessions = await sessions.ListByDeliveryChainIdAsync(id);
            if (chainSessions.Count == 0)
                return Results.NotFound();

            var chainTasks = new List<WorkTask>();
            foreach (var chainSession in chainSessions)
                chainTasks.AddRange(await tasks.ListBySessionAsync(chainSession.Id));

            var chainLeases = new List<ExecutionLease>();
            foreach (var chainTask in chainTasks)
                chainLeases.AddRange(await leases.ListByTaskIdAsync(chainTask.Id));

            var queue = RoleDeliveryChainGate.BuildQueue(
                id,
                chainSessions[0].WorkspaceRoot,
                chainSessions,
                chainTasks,
                chainLeases,
                leaseOptions.Value);

            return Results.Ok(RoleDeliveryChainApiMapper.ToJson(queue));
        });

        app.MapPost("/api/delivery-chains/{id:guid}/next-external", async (
            Guid id,
            DeliveryChainExternalNextRequest req,
            OrchestrationService orch) =>
        {
            try
            {
                var result = await orch.DispatchDeliveryChainExternalNextAsync(id, req.Agent);
                return Results.Ok(new
                {
                    roleName = result.Session.Name,
                    taskId = result.Task.Id,
                    branchName = result.BranchName,
                    workspacePath = result.WorkspacePath,
                    nextHumanAction = result.NextHumanAction,
                    prompt = result.Prompt,
                    executionDriver = result.Task.ExecutionDriver,
                    taskExecutionMode = result.Task.TaskExecutionMode,
                    status = result.Task.Status,
                });
            }
            catch (Exception ex)
            {
                return LeaseApiResults.FromException(ex);
            }
        });

        app.MapGet("/api/delivery-chains/{id:guid}/prompt", async (
            Guid id,
            IOperatorSessionRepository sessions,
            IWorkTaskRepository tasks,
            IExecutionLeaseRepository leases,
            ExternalTaskExecutionService external,
            IOptions<LeaseRuntimeOptions> leaseOptions) =>
        {
            var chainSessions = await sessions.ListByDeliveryChainIdAsync(id);
            if (chainSessions.Count == 0)
                return Results.NotFound();

            var chainTasks = new List<WorkTask>();
            foreach (var chainSession in chainSessions)
                chainTasks.AddRange(await tasks.ListBySessionAsync(chainSession.Id));

            var chainLeases = new List<ExecutionLease>();
            foreach (var chainTask in chainTasks)
                chainLeases.AddRange(await leases.ListByTaskIdAsync(chainTask.Id));

            var queue = RoleDeliveryChainGate.BuildQueue(
                id, chainSessions[0].WorkspaceRoot, chainSessions, chainTasks, chainLeases, leaseOptions.Value);

            var taskId = queue.NextTaskId
                ?? chainTasks.FirstOrDefault(t => t.Status == WorkTaskStatus.ExternalInProgress)?.Id;
            if (taskId is null)
                return Results.Json(new { error = "no_active_role", message = queue.BlockReason }, statusCode: 409);

            try
            {
                var prompt = await external.GetPromptAsync(taskId.Value);
                return Results.Ok(new { taskId, prompt });
            }
            catch (Exception ex)
            {
                return LeaseApiResults.FromException(ex);
            }
        });

        app.MapPost("/api/sessions/{id:guid}/authority/reconcile", async (
            Guid id,
            AuthorityAutopilotService autopilot,
            KanbanExecutionOrchestrator orchestrator,
            OrchestrationService orch,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var canonical = await orch.ResolveCanonicalSessionAsync(id, cancellationToken);
                var report = await autopilot.ReconcileReadyForReviewAsync(
                    (cardId, ct) => orchestrator.AcceptResultAsync(cardId, StatusChangeActor.System, ct),
                    canonical.Id,
                    cancellationToken);
                return Results.Ok(report);
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound();
            }
        });

        app.MapPost("/api/authority/reconcile-ready", async (
            AuthorityAutopilotService autopilot,
            KanbanExecutionOrchestrator orchestrator,
            CancellationToken cancellationToken) =>
        {
            var report = await autopilot.ReconcileReadyForReviewAsync(
                (cardId, ct) => orchestrator.AcceptResultAsync(cardId, StatusChangeActor.System, ct),
                sessionId: null,
                cancellationToken);
            return Results.Ok(report);
        });

        app.MapGet("/api/operational-modes", () =>
            Results.Ok(new
            {
                modes = JoyZoningOperationalModes.All.Select(m => new
                {
                    slug = m.Slug,
                    title = m.Title,
                    primaryQuestion = m.PrimaryQuestion,
                    shortDescription = m.ShortDescription,
                    mentalModel = m.MentalModel,
                    canonicalMetaphor = m.CanonicalMetaphor,
                    isCanonicalOperationalSurface = m.IsCanonicalOperationalSurface,
                    canonicalSurfaces = m.CanonicalSurfaces,
                    forbiddenInMode = m.ForbiddenInMode,
                    primaryEntities = m.PrimaryEntities,
                    desktopSurfaces = m.DesktopSurfaces,
                    watchComponents = m.WatchComponents,
                    apiRouteHints = m.ApiRouteHints,
                    allowsKanbanMutation = OperationalModeGuardrails.AllowsKanbanMutation(m.Slug),
                    allowsMergeApproveRevoke = OperationalModeGuardrails.AllowsMergeApproveRevoke(m.Slug),
                    allowsAuthoritativeWorkspaceActions = OperationalModeGuardrails.AllowsAuthoritativeWorkspaceActions(m.Slug),
                }),
                registryTransitions = OperationalModeNavigation.StandardRegistryTransitions().Select(t => new
                {
                    targetMode = t.TargetModeSlug,
                    t.Label,
                    t.Reason,
                }),
            }));

        app.MapGet("/api/sessions/{sessionId:guid}/workers/{executionSessionId:guid}/decision-preflight", async (
            Guid sessionId,
            Guid executionSessionId,
            string? action,
            WorkspaceWorkerObservabilityService observability,
            OrchestrationService orch,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(action))
                return Results.BadRequest(new { error = "invalid_action", message = "Query action=accept|revoke|inspect is required (approve is a legacy alias for accept)." });

            try
            {
                var canonical = await orch.ResolveCanonicalSessionAsync(sessionId, cancellationToken);
                var model = await observability.GetDecisionPreflightAsync(
                    canonical.Id,
                    executionSessionId,
                    action,
                    cancellationToken);
                return Results.Ok(OperatorDecisionApiMapper.MapPreflight(model));
            }
            catch (InvalidOperationException ex)
            {
                return Results.NotFound(new { error = "not_found", message = ex.Message });
            }
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
            IWorkTaskRepository tasks,
            KanbanExecutionOrchestrator exec,
            ExternalTaskExecutionService external) =>
        {
            try
            {
                if (req.Report.CommandsRun.Count == 0)
                    return Results.Json(
                        new { error = "lease_invalid_request", message = "commandsRun is required." },
                        statusCode: 400);

                var task = await tasks.GetByIdAsync(id);
                if (task?.TaskExecutionMode == TaskExecutionMode.ExternalAgent)
                    return Results.Ok(await external.SubmitVerificationAsync(id, req.Report));

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

        app.MapPost("/api/tasks/{id:guid}/external/start", async (
            Guid id,
            StartExternalTaskRequest req,
            ExternalTaskExecutionService external) =>
        {
            try
            {
                var result = await external.StartExternalAsync(id, req.Agent);
                return Results.Ok(new
                {
                    roleName = result.Session.Name,
                    taskId = result.Task.Id,
                    branchName = result.BranchName,
                    workspacePath = result.WorkspacePath,
                    nextHumanAction = result.NextHumanAction,
                    prompt = result.Prompt,
                    status = result.Task.Status,
                    executionDriver = result.Task.ExecutionDriver,
                    taskExecutionMode = result.Task.TaskExecutionMode,
                });
            }
            catch (Exception ex)
            {
                return LeaseApiResults.FromException(ex);
            }
        });

        app.MapGet("/api/tasks/{id:guid}/external/prompt", async (
            Guid id,
            ExternalTaskExecutionService external) =>
        {
            try
            {
                var prompt = await external.GetPromptAsync(id);
                return Results.Ok(new { taskId = id, prompt });
            }
            catch (Exception ex)
            {
                return LeaseApiResults.FromException(ex);
            }
        });

        app.MapGet("/api/tasks/{id:guid}/external/status", async (
            Guid id,
            bool? refresh,
            ExternalTaskExecutionService external) =>
        {
            try
            {
                var status = await external.GetStatusAsync(id, refresh == true);
                return Results.Ok(MapExternalStatus(status));
            }
            catch (Exception ex)
            {
                return LeaseApiResults.FromException(ex);
            }
        });

        app.MapGet("/api/tasks/{id:guid}/workspace/status", async (
            Guid id,
            ExternalTaskExecutionService external) =>
        {
            try
            {
                var status = await external.GetStatusAsync(id, refreshWorkspace: true);
                return Results.Ok(MapWorkspaceStatus(status));
            }
            catch (Exception ex)
            {
                return LeaseApiResults.FromException(ex);
            }
        });

        app.MapPost("/api/tasks/{id:guid}/external/ready-for-review", async (
            Guid id,
            ExternalTaskExecutionService external) =>
        {
            try
            {
                var task = await external.MarkReadyForReviewAsync(id);
                return Results.Ok(task);
            }
            catch (Exception ex)
            {
                return LeaseApiResults.FromException(ex);
            }
        });

        app.MapPost("/api/tasks/{id:guid}/external/verify", async (
            Guid id,
            SubmitVerificationRequest req,
            ExternalTaskExecutionService external) =>
        {
            try
            {
                if (req.Report.CommandsRun.Count == 0)
                    return Results.Json(
                        new { error = "external_task_invalid_request", message = "commandsRun is required." },
                        statusCode: 400);

                return Results.Ok(await external.SubmitVerificationAsync(id, req.Report));
            }
            catch (Exception ex)
            {
                return LeaseApiResults.FromException(ex);
            }
        });

        app.MapPost("/api/tasks/{id:guid}/external/complete", async (
            Guid id,
            ExternalTaskCompleteRequest? req,
            ExternalTaskExecutionService external) =>
        {
            try
            {
                var approved = req?.OperatorApproved == true;
                return Results.Ok(await external.CompleteExternalAsync(
                    id, approved, req?.AlreadyMerged == true));
            }
            catch (Exception ex)
            {
                return LeaseApiResults.FromException(ex);
            }
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

        app.MapGet("/api/workspace/changed", async (
            string workspaceRoot,
            Guid? sessionId,
            IWorkspaceAdapter workspace,
            WorkspaceEventPublisher workspaceEvents,
            CancellationToken cancellationToken) =>
        {
            var files = await workspace.ListChangedFilesAsync(workspaceRoot, cancellationToken);
            await workspaceEvents.PublishChangedFilesAsync(
                workspaceRoot,
                files,
                sessionId,
                taskId: null,
                cancellationToken);
            return Results.Ok(files);
        });

        app.MapGet("/api/workspace/diff", async (string workspaceRoot, string path, IWorkspaceAdapter workspace) =>
        {
            var diff = await workspace.GetGitDiffAsync(workspaceRoot, path);
            return diff is null
                ? Results.NotFound(new { message = "Not a git repo or no diff for path." })
                : Results.Ok(new { path, diff });
        });

        app.MapGet("/api/tasks/{id:guid}/workspace/changed", async (
            Guid id,
            Guid? sessionId,
            IWorkTaskRepository tasks,
            IOperatorSessionRepository sessions,
            KanbanExecutionOrchestrator exec,
            IWorkspaceAdapter workspace,
            WorkspaceEventPublisher workspaceEvents,
            CancellationToken cancellationToken) =>
        {
            var target = await WorkspaceInspection.ResolveForTaskAsync(
                id, tasks, sessions, exec, cancellationToken);
            if (target is null)
                return Results.NotFound(new { error = "task_not_found", message = "Task or workspace root not found." });

            var files = await workspace.ListChangedFilesAsync(target.Root, cancellationToken);
            await workspaceEvents.PublishChangedFilesAsync(
                target.Root,
                files,
                sessionId ?? target.SessionId,
                taskId: id,
                cancellationToken);
            return Results.Ok(new
            {
                taskId = id,
                workspaceRoot = target.Root,
                inspect = target.IsWorktree ? "isolated" : "canonical",
                files,
            });
        });

        app.MapGet("/api/tasks/{id:guid}/workspace/tree", async (
            Guid id,
            IWorkTaskRepository tasks,
            IOperatorSessionRepository sessions,
            KanbanExecutionOrchestrator exec,
            IWorkspaceAdapter workspace,
            CancellationToken cancellationToken) =>
        {
            var target = await WorkspaceInspection.ResolveForTaskAsync(
                id, tasks, sessions, exec, cancellationToken);
            if (target is null)
                return Results.NotFound(new { error = "task_not_found", message = "Task or workspace root not found." });

            var tree = await workspace.ListTreeAsync(target.Root, cancellationToken: cancellationToken);
            return Results.Ok(new
            {
                taskId = id,
                workspaceRoot = target.Root,
                inspect = target.IsWorktree ? "worktree" : "session",
                tree,
            });
        });

        app.MapGet("/api/tasks/{id:guid}/workspace/diff", async (
            Guid id,
            string path,
            IWorkTaskRepository tasks,
            IOperatorSessionRepository sessions,
            KanbanExecutionOrchestrator exec,
            IWorkspaceAdapter workspace,
            CancellationToken cancellationToken) =>
        {
            var target = await WorkspaceInspection.ResolveForTaskAsync(
                id, tasks, sessions, exec, cancellationToken);
            if (target is null)
                return Results.NotFound(new { error = "task_not_found", message = "Task or workspace root not found." });

            var diff = await workspace.GetGitDiffAsync(target.Root, path, cancellationToken);
            return diff is null
                ? Results.NotFound(new { message = "Not a git repo or no diff for path." })
                : Results.Ok(new
                {
                    taskId = id,
                    workspaceRoot = target.Root,
                    inspect = target.IsWorktree ? "worktree" : "session",
                    path,
                    diff,
                });
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
                apiUrl = runtime.GetSnapshot().ApiBaseUrl,
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

        app.MapPost("/api/hermes/sync-credentials", async (HermesConnectivityService connectivity) =>
        {
            var report = await connectivity.SyncCredentialsAsync();
            return report.Ok
                ? Results.Ok(new { ok = true, message = report.Message })
                : Results.Json(new { ok = false, message = report.Message }, statusCode: StatusCodes.Status409Conflict);
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

        app.MapGet("/api/hermes/connector-status", async (HermesConnectorDiagnostics diagnostics) =>
        {
            var report = await diagnostics.EvaluateAsync();
            return Results.Ok(new
            {
                overall = report.Overall.ToString(),
                checks = report.Checks.Select(c => new { id = c.Id, state = c.State.ToString(), detail = c.Detail }),
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
                outcome = state.LastOutcome?.ToString(),
                lastAuthFailureAt = state.LastAuthFailureAt,
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
            OrchestrationService orch,
            IExecutionRepository executions) =>
        {
            var execution = await executions.GetByIdAsync(id);
            if (execution is null)
                return Results.NotFound();
            await orch.MarkExecutionCancelledAsync(id);
            return Results.Ok(execution);
        });

        app.MapGet("/api/agent/manifest", () =>
            Results.Ok(AgentOperationsManifest.BuildStatic()));

        app.MapGet("/api/agent/context", async (
            IOperatorSessionRepository sessions,
            ApprovalService approvals,
            IExecutionLeaseRepository leases,
            CancellationToken cancellationToken) =>
        {
            var sessionList = await sessions.ListAsync(cancellationToken);
            var pending = await approvals.ListPendingAsync(cancellationToken);
            var activeLeases = await leases.ListActiveAsync(cancellationToken);
            var blockedLeases = activeLeases.Count(l => l.Status == ExecutionLeaseStatus.Blocked);

            return Results.Ok(new
            {
                manifestVersion = AgentOperationsManifest.ManifestVersion,
                fingerprint = AgentOperationsManifestCache.ComputeStaticFingerprint(),
                generatedAt = DateTimeOffset.UtcNow,
                service = "joyzoning-control-plane",
                health = "ok",
                sessions = new { total = sessionList.Count },
                leases = new { active = activeLeases.Count, blocked = blockedLeases },
                pendingApprovals = new { count = pending.Count, available = true },
                entrypoints = AgentOperationsManifest.Entrypoints,
                importantFiles = AgentOperationsManifest.ImportantFiles,
                protectedPaths = AgentOperationsManifest.ProtectedPaths,
                workflow = AgentOperationsManifest.AgentWorkflow,
                http = AgentOperationsManifest.HttpSurfaces,
                manifestCache = AgentOperationsManifestCache.RelativePath,
            });
        });

        app.MapGet("/api/agent/endpoints", (bool? agentSafe) =>
        {
            var endpoints = agentSafe == true
                ? JoyZoningEndpointRegistry.Endpoints.Where(e => e.AgentSafe).ToList()
                : JoyZoningEndpointRegistry.Endpoints;
            return Results.Ok(new
            {
                manifestVersion = AgentOperationsManifest.ManifestVersion,
                agentSafeOnly = agentSafe == true,
                count = endpoints.Count,
                endpoints,
            });
        });
    }

    private static object MapExternalStatus(ExternalTaskStatusResponse status) => new
    {
        taskId = status.TaskId,
        status = status.Status,
        taskExecutionMode = status.TaskExecutionMode,
        executionDriver = status.ExecutionDriver,
        externalAgentName = status.ExternalAgentName,
        branchName = status.BranchName,
        workspacePath = status.WorkspacePath,
        startedExternallyAt = status.StartedExternallyAt,
        readyForReviewAt = status.ReadyForReviewAt,
        lastWorkspaceScanAt = status.LastWorkspaceScanAt,
        verificationStatus = status.VerificationStatus,
        mergeRequired = status.MergeRequired,
        externalMergeCompleted = status.ExternalMergeCompleted,
        blockedReason = status.BlockedReason,
        readyForReviewAllowed = status.Status == WorkTaskStatus.ExternalInProgress
            && status.Workspace?.BranchMatches == true
            && status.Workspace?.HasChanges == true,
        workspace = status.Workspace is null ? null : MapWorkspaceScan(status.Workspace),
    };

    private static object MapWorkspaceStatus(ExternalTaskStatusResponse status)
    {
        var scan = status.Workspace ?? new TaskWorkspaceScanResult(
            status.TaskId,
            status.WorkspacePath ?? string.Empty,
            status.BranchName,
            null,
            false,
            false,
            Array.Empty<string>(),
            null,
            status.LastWorkspaceScanAt ?? DateTimeOffset.UtcNow,
            false,
            Array.Empty<string>(),
            Array.Empty<string>());

        return new
        {
            taskId = status.TaskId,
            workspacePath = scan.WorkspacePath,
            branchName = scan.ExpectedBranchName,
            currentBranch = scan.CurrentBranch,
            branchMatches = scan.BranchMatches,
            hasChanges = scan.HasChanges,
            changedFiles = scan.ChangedFiles,
            lastCommit = scan.LastCommit,
            lastScanAt = scan.ScannedAt,
            executionDriver = status.ExecutionDriver,
            taskExecutionMode = status.TaskExecutionMode,
            status = status.Status,
            readyForReviewAllowed = status.Status == WorkTaskStatus.ExternalInProgress
                && scan.BranchMatches
                && scan.HasChanges,
            blockedReason = status.BlockedReason,
        };
    }

    private static object MapWorkspaceScan(TaskWorkspaceScanResult scan) => new
    {
        taskId = scan.TaskId,
        workspacePath = scan.WorkspacePath,
        branchName = scan.ExpectedBranchName,
        currentBranch = scan.CurrentBranch,
        branchMatches = scan.BranchMatches,
        hasChanges = scan.HasChanges,
        changedFiles = scan.ChangedFiles,
        lastCommit = scan.LastCommit,
        lastScanAt = scan.ScannedAt,
        hasUncommittedChanges = scan.HasUncommittedChanges,
        stagedFiles = scan.StagedFiles,
        untrackedFiles = scan.UntrackedFiles,
    };
}

public record OpenWorkspacePathRequest(string Path);
public record ImportKanbanRequest(Guid SessionId);
public record EnsureDashboardRequest(bool AlsoEnsureGateway = true);

public record CreateSessionRequest(string Name, string WorkspaceRoot, string? HermesProfile);
public record CreateDeliveryChainRequest(
    string ProgramName,
    string WorkspaceRoot,
    string? HermesProfile = null,
    IReadOnlyList<CreateDeliveryChainRoleRequest>? Roles = null);
public record CreateDeliveryChainRoleRequest(
    string Title,
    string? Description = null,
    AgentKind AssignedAgent = AgentKind.DietCode,
    RiskLevel Risk = RiskLevel.Low);
public record DeliveryChainExternalNextRequest(string Agent);
public record StartExternalTaskRequest(string Agent);
public record ExternalTaskCompleteRequest(bool OperatorApproved = false, bool AlreadyMerged = false);
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
