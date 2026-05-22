using System.Diagnostics;
using System.Text.Json;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;

namespace JoyZoning.Cli;

public static class YoloRunner
{
    public static async Task<object> PlanAsync(
        IYoloRunClient client,
        YoloPolicy policy,
        CancellationToken cancellationToken = default)
    {
        var candidates = await QueryCandidatesAsync(client, policy, cancellationToken);
        return new
        {
            ok = true,
            dryRun = true,
            sessionId = policy.SessionId,
            maxRiskLevel = policy.MaxRiskLevel,
            pickupStatuses = YoloEligibility.GetPickupStatuses(policy).Select(s => s.ToString()).ToList(),
            selected = candidates.Where(c => c.IsEligible).Select(ToPlanEntry).ToList(),
            skipped = candidates.Where(c => !c.IsEligible).Select(ToPlanEntry).ToList(),
        };
    }

    public static async Task<object> RunAsync(
        IYoloRunClient client,
        CliArgs args,
        YoloPolicy policy,
        CancellationToken cancellationToken = default)
    {
        foreach (var cmd in policy.RequiredVerificationCommands)
            policy.AssertCommandAllowed(cmd);

        var existing = YoloRunState.TryLoad();
        if (existing is { StopRequested: false, StoppedAt: null } &&
            existing.Phase is "running" or "dispatching" or "verifying" or "stopping")
            throw new CliUsageException(
                "A YOLO run is already active. Run jz yolo stop first or wait for it to finish.");

        var runState = new YoloRunState
        {
            RunId = Guid.NewGuid(),
            SessionId = policy.SessionId,
            PolicyPath = args.Opt("--policy") ?? "",
            Phase = "running",
            StartedAt = DateTimeOffset.UtcNow,
        };
        runState.Save();

        var audit = new YoloAuditLog(runState.RunId);
        runState.AuditLogPath = audit.FilePath;
        runState.Save();

        audit.Append(YoloEvidenceKinds.Started, "YOLO autonomous run started.",
            new { policy.MaxTasksPerRun, policy.MaxRiskLevel, policy.SessionId });

        var deadline = runState.StartedAt.AddMinutes(policy.MaxRuntimeMinutes);
        var tasksDone = 0;
        var tasksSkipped = 0;
        string? stopReason = null;

        try
        {
            var candidates = await QueryCandidatesAsync(client, policy, cancellationToken);
            var eligible = candidates.Where(c => c.IsEligible).ToList();

            foreach (var skip in candidates.Where(c => !c.IsEligible))
            {
                tasksSkipped++;
                audit.Append(YoloEvidenceKinds.TaskSkipped, skip.SkipReason ?? "Skipped",
                    new { taskId = skip.TaskId, skip.SkipReason });
                await RecordTaskEvidenceAsync(client, skip.TaskId, YoloEvidenceKinds.TaskSkipped,
                    skip.SkipReason ?? "Skipped", new { runId = runState.RunId });
            }

            foreach (var task in eligible)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RefreshStopFlag(runState);

                if (runState.StopRequested)
                {
                    stopReason = "Stop requested.";
                    break;
                }

                if (DateTimeOffset.UtcNow >= deadline)
                {
                    stopReason = "maxRuntimeMinutes exceeded.";
                    break;
                }

                if (tasksDone >= policy.MaxTasksPerRun)
                {
                    stopReason = "maxTasksPerRun reached.";
                    break;
                }

                runState.CurrentTaskId = task.TaskId;
                runState.Phase = "dispatching";
                runState.Save();

                audit.Append(YoloEvidenceKinds.TaskSelected, "YOLO selected task.",
                    new { task.TaskId, task.Title, task.Risk, task.Tags });
                await RecordTaskEvidenceAsync(client, task.TaskId, YoloEvidenceKinds.TaskSelected,
                    "YOLO selected task for autonomous run.", new { runId = runState.RunId, task.Title });

                var outcome = await RunOneTaskAsync(client, args, policy, task, audit, cancellationToken);
                if (outcome.ShouldStopRun)
                {
                    stopReason = outcome.Reason;
                    break;
                }

                if (outcome.WasSkipped)
                    tasksSkipped++;
                else
                    tasksDone++;
            }
        }
        catch (YoloPolicyViolationException ex)
        {
            runState.LastError = ex.Message;
            runState.Phase = "policy_violation";
            audit.Append(YoloEvidenceKinds.PolicyViolation, ex.Message, new { runState.RunId });
            if (runState.CurrentTaskId is { } tid)
                await RecordTaskEvidenceAsync(client, tid, YoloEvidenceKinds.PolicyViolation, ex.Message, null);
            throw;
        }
        catch (Exception ex) when (ex is not CliUsageException and not OperationCanceledException)
        {
            runState.LastError = ex.Message;
            runState.Phase = "error";
            stopReason = $"Unexpected error: {ex.Message}";
            audit.Append(YoloEvidenceKinds.Stopped, stopReason, new { error = ex.Message });
            throw;
        }
        finally
        {
            runState.TasksCompleted = tasksDone;
            runState.TasksSkipped = tasksSkipped;
            runState.CurrentTaskId = null;
            if (runState.Phase is "running" or "dispatching" or "verifying")
                runState.Phase = stopReason is null ? "completed" : "stopped";
            if (runState.StopRequested && runState.Phase != "policy_violation")
                runState.Phase = "stopped";
            runState.StoppedAt = DateTimeOffset.UtcNow;
            runState.Save();

            audit.Append(YoloEvidenceKinds.Stopped,
                stopReason ?? "YOLO run finished.",
                new { runState.Phase, tasksDone, tasksSkipped, stopReason });
        }

        return new
        {
            ok = true,
            runId = runState.RunId,
            phase = runState.Phase,
            auditLogPath = audit.FilePath,
            tasksCompleted = tasksDone,
            tasksSkipped,
            stopReason,
            lastError = runState.LastError,
        };
    }

    public static object Status()
    {
        var state = YoloRunState.TryLoad();
        if (state is null)
            return new { ok = true, active = false, phase = "idle" };

        return new
        {
            ok = true,
            active = state.StoppedAt is null && state.Phase is
                "running" or "dispatching" or "verifying" or "stopping",
            state.RunId,
            state.SessionId,
            state.PolicyPath,
            state.AuditLogPath,
            state.Phase,
            state.StopRequested,
            state.StartedAt,
            state.StoppedAt,
            state.CurrentTaskId,
            state.TasksCompleted,
            state.TasksSkipped,
            state.LastError,
        };
    }

    public static object Stop()
    {
        var state = YoloRunState.TryLoad();
        if (state is null)
            return new { ok = true, message = "No active YOLO run state." };

        state.StopRequested = true;
        state.Phase = "stopping";
        state.Save();
        return new { ok = true, message = "Stop requested; run will exit after current step.", state.RunId };
    }

    private static async Task<YoloTaskOutcome> RunOneTaskAsync(
        IYoloRunClient client,
        CliArgs args,
        YoloPolicy policy,
        YoloTaskCandidate task,
        YoloAuditLog audit,
        CancellationToken cancellationToken)
    {
        if (YoloEligibility.IsCriticalRisk(task.Risk))
        {
            await RecordTaskEvidenceAsync(client, task.TaskId, YoloEvidenceKinds.TaskSkipped,
                "Critical/high risk requires prior human approval.", new { task.Risk });
            return YoloTaskOutcome.Skip("critical_requires_human_approval");
        }

        var sessionId = policy.SessionId;
        var leaseBefore = await client.GetLeaseAsync(task.TaskId);
        CliHttpResult dispatchResult;

        if (leaseBefore.IsSuccess && leaseBefore.Body is not null)
        {
            var leaseElBefore = leaseBefore.Body.Value;
            YoloLeaseJson.TryReadStatus(leaseElBefore, out var beforeStatus);
            var leaseRisk = leaseElBefore.TryGetProperty("riskLevel", out var rp)
                ? (LeaseRiskLevel)rp.GetInt32()
                : LeaseRiskLevel.Low;
            var granted = leaseElBefore.TryGetProperty("criticalApprovalGranted", out var g) && g.GetBoolean();
            var consumed = leaseElBefore.TryGetProperty("criticalApprovalConsumed", out var c) && c.GetBoolean();

            if (YoloEligibility.LeaseHasPreApprovedCritical(leaseRisk, granted, consumed))
            {
                await RecordTaskEvidenceAsync(client, task.TaskId, YoloEvidenceKinds.TaskSkipped,
                    "Critical lease requires human dispatch.", new { preApproved = true });
                return YoloTaskOutcome.Skip("critical_preapproved_not_yolo_dispatch");
            }

            if (beforeStatus == ExecutionLeaseStatus.ReadyForReview)
            {
                await RecordTaskEvidenceAsync(client, task.TaskId, YoloEvidenceKinds.TaskSkipped,
                    "Lease already ready for human review.", null);
                return YoloTaskOutcome.Skip("already_ready_for_review");
            }

            if (beforeStatus == ExecutionLeaseStatus.Blocked && policy.AllowRecover)
            {
                audit.Append(YoloEvidenceKinds.RecoverAttempted, "Reopening blocked lease.",
                    new { task.TaskId });
                await RecordTaskEvidenceAsync(client, task.TaskId, YoloEvidenceKinds.RecoverAttempted,
                    "YOLO reopening blocked lease.", null);

                var recover = await client.RecoverLeaseAsync(
                    task.TaskId,
                    sessionId,
                    LeaseRecoveryMode.ReopenBlocked,
                    StatusChangeActor.Human,
                    humanApprovedCritical: false);
                if (!recover.IsSuccess)
                    return YoloTaskOutcome.Stop($"Recover failed: {recover.Message ?? recover.Error}");

                audit.Append(YoloEvidenceKinds.DispatchRetryAttempted, "Dispatch retry after recover.",
                    new { task.TaskId });
                await RecordTaskEvidenceAsync(client, task.TaskId, YoloEvidenceKinds.DispatchRetryAttempted,
                    "YOLO dispatch-retry after recover.", null);

                dispatchResult = await client.DispatchRetryAsync(
                    task.TaskId, sessionId, StatusChangeActor.Human, humanApprovedCritical: false);
            }
            else if (beforeStatus is ExecutionLeaseStatus.Leased
                     or ExecutionLeaseStatus.Running
                     or ExecutionLeaseStatus.Verifying)
            {
                dispatchResult = CliHttpResult.FromResponse(
                    System.Net.HttpStatusCode.OK,
                    """{"ok":true,"message":"lease active; waiting for executor"}""");
            }
            else
            {
                await RecordTaskEvidenceAsync(client, task.TaskId, YoloEvidenceKinds.TaskSkipped,
                    "Active lease in unsupported state.", new { status = beforeStatus.ToString() });
                return YoloTaskOutcome.Skip("active_lease_unsupported");
            }
        }
        else
        {
            await RecordTaskEvidenceAsync(client, task.TaskId, YoloEvidenceKinds.DispatchAttempted,
                "YOLO dispatching task.", new { humanApprovedCritical = false });

            dispatchResult = await client.DispatchTaskAsync(task.TaskId, humanApprovedCritical: false);
        }

        if (!dispatchResult.IsSuccess)
            return YoloTaskOutcome.Stop($"Dispatch failed: {dispatchResult.Message ?? dispatchResult.Error}");

        var wait = await WaitForExecutorAsync(client, policy, task.TaskId, sessionId, audit, cancellationToken);
        if (!wait.Ready)
            return YoloTaskOutcome.Stop(wait.Reason ?? "Executor did not become ready.");

        var leaseResult = await client.GetLeaseAsync(task.TaskId);
        if (!leaseResult.IsSuccess || leaseResult.Body is null)
            return YoloTaskOutcome.Stop("Lease not found after dispatch.");

        var leaseEl = leaseResult.Body.Value;
        var worktree = YoloLeaseJson.ReadString(leaseEl, "worktreePath") ?? "";
        if (string.IsNullOrWhiteSpace(worktree))
            return YoloTaskOutcome.Stop("Lease missing worktree path.");

        sessionId = YoloLeaseJson.ReadGuid(leaseEl, "assignedSessionId")
            ?? YoloLeaseJson.ReadGuid(leaseEl, "operatorSessionId")
            ?? policy.SessionId;

        if (policy.RequireCleanWorktreeBeforeStart && !IsWorktreeClean(worktree))
            return YoloTaskOutcome.Stop($"Worktree not clean: {worktree}");

        var verificationCommands = YoloVerificationCommands.Resolve(policy, leaseEl);
        var harnessArgs = CloneArgsForHarness(args, sessionId, verificationCommands);

        await YoloAgentHarness.StartAsync(client, harnessArgs, task.TaskId, cancellationToken);
        await client.HeartbeatLeaseAsync(task.TaskId, sessionId, StatusChangeActor.DietCode);

        var prevDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(worktree);
            var fileCtx = JoyZoningRuntimeContext.TryLoad(worktree);
            if (fileCtx is null)
                return YoloTaskOutcome.Stop("Could not load runtime context after dispatch.");

            runStatePhaseVerifying(task.TaskId);

            var passed = await RunVerificationWithRetriesAsync(
                client, harnessArgs, fileCtx, policy, verificationCommands, cancellationToken);

            if (!passed)
            {
                var reason = $"Verification failed after {policy.MaxVerificationRetries + 1} attempt(s).";
                await RecordTaskEvidenceAsync(client, task.TaskId, YoloEvidenceKinds.Blocked, reason,
                    new { policy.MaxVerificationRetries });
                await client.AgentLeaseStatusAsync(task.TaskId, ExecutionLeaseStatus.Blocked, reason);
                return YoloTaskOutcome.Continue();
            }

            var done = await YoloAgentHarness.DoneAsync(client, harnessArgs, fileCtx, cancellationToken);
            if (!done.IsSuccess)
                return YoloTaskOutcome.Stop(done.Message ?? done.Error ?? "ready_for_review submission failed");

            await RecordTaskEvidenceAsync(client, task.TaskId, YoloEvidenceKinds.ReadyForReview,
                "YOLO submitted passing verification; ready for human review (not Complete).",
                new { requireHumanMerge = policy.RequireHumanMerge });

            return YoloTaskOutcome.Continue();
        }
        finally
        {
            Directory.SetCurrentDirectory(prevDir);
        }
    }

    private static void runStatePhaseVerifying(Guid taskId)
    {
        var state = YoloRunState.TryLoad();
        if (state is null)
            return;
        state.Phase = "verifying";
        state.CurrentTaskId = taskId;
        state.Save();
    }

    private static async Task<(bool Ready, string? Reason)> WaitForExecutorAsync(
        IYoloRunClient client,
        YoloPolicy policy,
        Guid taskId,
        Guid sessionId,
        YoloAuditLog audit,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(policy.DispatchSettleTimeoutSeconds);
        var lastHeartbeat = DateTimeOffset.MinValue;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RefreshStopFlagForWait();

            var lease = await client.GetLeaseAsync(taskId);
            if (!lease.IsSuccess || lease.Body is null)
                return (false, "Lease disappeared while waiting for executor.");

            if (!YoloLeaseJson.TryReadStatus(lease.Body.Value, out var status))
                return (false, "Lease status missing.");

            if (YoloExecutorWait.IsTerminalFailure(status))
                return (false, $"Lease became terminal ({status}) during executor wait.");

            if (YoloExecutorWait.IsReadyForVerification(status))
            {
                audit.Append(YoloEvidenceKinds.ExecutorWait, "Executor ready for verification.",
                    new { taskId, status = status.ToString() });
                return (true, null);
            }

            var now = DateTimeOffset.UtcNow;
            if ((now - lastHeartbeat).TotalSeconds >= policy.HeartbeatIntervalSeconds)
            {
                await client.HeartbeatLeaseAsync(taskId, sessionId, StatusChangeActor.DietCode);
                lastHeartbeat = now;
            }

            await Task.Delay(TimeSpan.FromSeconds(policy.DispatchPollIntervalSeconds), cancellationToken);
        }

        return (false, $"Executor not ready within {policy.DispatchSettleTimeoutSeconds}s.");
    }

    private static void RefreshStopFlagForWait()
    {
        var disk = YoloRunState.TryLoad();
        if (disk?.StopRequested == true)
            throw new OperationCanceledException("YOLO stop requested.");
    }

    private static async Task<bool> RunVerificationWithRetriesAsync(
        IYoloRunClient client,
        CliArgs args,
        JoyZoningRuntimeContext ctx,
        YoloPolicy policy,
        IReadOnlyList<string> verificationCommands,
        CancellationToken cancellationToken)
    {
        var attempts = policy.MaxVerificationRetries + 1;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (_, runs, allPassed) = await YoloAgentHarness.VerifyAsync(
                client, args, ctx, policy, verificationCommands, cancellationToken);

            if (allPassed)
                return true;

            if (attempt < attempts)
            {
                await client.RecordAgentEvidenceAsync(ctx.TaskId, YoloEvidenceKinds.VerifyRetry,
                    $"Verification attempt {attempt} failed; retrying.",
                    new { attempt, failed = runs.Where(r => !r.Passed).Select(r => r.Command).ToList() });
            }
        }

        return false;
    }

    private static async Task<IReadOnlyList<YoloTaskCandidate>> QueryCandidatesAsync(
        IYoloRunClient client,
        YoloPolicy policy,
        CancellationToken cancellationToken)
    {
        var list = await client.ListTasksAsync(policy.SessionId);
        if (!list.IsSuccess || list.Body is null)
        {
            throw new CliUsageException(
                list.Message ?? list.Error ?? list.RawText
                ?? $"Could not list tasks for session {policy.SessionId} (HTTP {(int)list.StatusCode}).");
        }

        if (list.Body.Value.ValueKind != JsonValueKind.Array)
            throw new CliUsageException("Unexpected tasks list response.");

        var results = new List<YoloTaskCandidate>();
        foreach (var el in list.Body.Value.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var taskId = YoloLeaseJson.ReadGuid(el, "id") ?? Guid.Empty;
            if (taskId == Guid.Empty)
                continue;

            var title = YoloLeaseJson.ReadString(el, "title") ?? "";
            var description = YoloLeaseJson.ReadString(el, "description");
            var status = el.TryGetProperty("status", out var st) ? st.GetInt32() : 0;
            var risk = el.TryGetProperty("risk", out var rk) ? rk.GetInt32() : 0;

            ExecutionLeaseStatus? leaseStatus = null;
            var hasActive = false;
            var lease = await client.GetLeaseAsync(taskId);
            if (lease.IsSuccess && lease.Body is not null
                && YoloLeaseJson.TryReadStatus(lease.Body.Value, out var ls))
            {
                leaseStatus = ls;
                hasActive = YoloLeaseJson.IsActiveStatus(ls)
                    && !(ls == ExecutionLeaseStatus.Blocked && policy.AllowRecover);
            }

            results.Add(YoloEligibility.Evaluate(
                taskId, title, description, status, risk, hasActive, leaseStatus, policy));
        }

        return results
            .OrderBy(c => c.Risk)
            .ThenBy(c => c.TaskId)
            .ToList();
    }

    private static bool IsWorktreeClean(string worktreePath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
                Arguments = OperatingSystem.IsWindows()
                    ? $"/c git -C \"{worktreePath}\" status --porcelain"
                    : $"-c \"git -C '{worktreePath.Replace("'", "'\\''")}' status --porcelain\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p is null)
                return false;
            var stdout = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return p.ExitCode == 0 && string.IsNullOrWhiteSpace(stdout);
        }
        catch
        {
            return false;
        }
    }

    private static Task RecordTaskEvidenceAsync(
        IYoloRunClient client,
        Guid taskId,
        string kind,
        string summary,
        object? detail) =>
        client.RecordAgentEvidenceAsync(taskId, kind, summary, detail);

    private static void RefreshStopFlag(YoloRunState runState)
    {
        var disk = YoloRunState.TryLoad();
        if (disk?.StopRequested == true)
            runState.StopRequested = true;
    }

    private static CliArgs CloneArgsForHarness(
        CliArgs args,
        Guid sessionId,
        IReadOnlyList<string> verificationCommands)
    {
        var raw = new List<string>(args.Raw);
        raw.RemoveAll(x => x == "--cmd" || x.StartsWith("--cmd=", StringComparison.Ordinal));
        foreach (var cmd in verificationCommands)
        {
            raw.Add("--cmd");
            raw.Add(cmd);
        }

        return new CliArgs
        {
            Raw = raw.ToArray(),
            Positionals = args.Positionals,
            BaseUrl = args.BaseUrl,
            SessionId = sessionId,
            TaskId = args.TaskId,
            PrettyJson = args.PrettyJson,
            Quiet = args.Quiet,
            Yes = args.Yes,
            FieldPath = args.FieldPath,
        };
    }

    private static object ToPlanEntry(YoloTaskCandidate c) => new
    {
        taskId = c.TaskId,
        title = c.Title,
        status = c.Status.ToString(),
        risk = c.Risk.ToString(),
        tags = c.Tags,
        skipReason = c.SkipReason,
    };

    private sealed record YoloTaskOutcome(bool WasSkipped, bool ShouldStopRun, string? Reason)
    {
        public static YoloTaskOutcome Continue() => new(false, false, null);
        public static YoloTaskOutcome Skip(string reason) => new(true, false, reason);
        public static YoloTaskOutcome Stop(string reason) => new(false, true, reason);
    }
}
