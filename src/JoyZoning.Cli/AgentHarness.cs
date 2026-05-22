using System.Text.Json;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;

namespace JoyZoning.Cli;

public static class AgentHarness
{
    public static async Task<CliHttpResult> StartAsync(
        JoyZoningCliClient client,
        CliArgs args,
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        var leaseResult = await client.GetLeaseAsync(taskId);
        if (!leaseResult.IsSuccess || leaseResult.Body is null)
            return leaseResult;

        var lease = leaseResult.Body.Value;
        var sessionId = ReadGuid(lease, "assignedSessionId") ?? ReadGuid(lease, "operatorSessionId")
            ?? args.RequireSessionId(null);

        var worktree = ReadString(lease, "worktreePath") ?? "";
        if (string.IsNullOrWhiteSpace(worktree))
            throw new CliUsageException("Lease has no worktree path.");

        var entity = MapLeaseElement(lease, sessionId, worktree);
        JoyZoningRuntimeContext.Write(entity, args.BaseUrl);

        await client.RecordAgentEvidenceAsync(taskId, "agent.started",
            "Agent harness bound to lease worktree.",
            new { worktree, contextFile = JoyZoningRuntimeContext.RelativePath });

        return leaseResult;
    }

    public static async Task<CliHttpResult> HeartbeatAsync(
        JoyZoningCliClient client,
        JoyZoningRuntimeContext ctx,
        CancellationToken cancellationToken = default)
    {
        AgentGuard.RequireWorktree(ctx);
        var hb = await client.HeartbeatLeaseAsync(ctx.TaskId, ctx.SessionId, StatusChangeActor.DietCode);
        if (hb.IsSuccess)
            await client.RecordAgentEvidenceAsync(ctx.TaskId, "agent.heartbeat", "Agent heartbeat recorded.");
        return hb;
    }

    public static async Task<(CliHttpResult Api, IReadOnlyList<CommandRunResult> Runs, bool AllPassed)> VerifyAsync(
        JoyZoningCliClient client,
        CliArgs args,
        JoyZoningRuntimeContext ctx,
        CancellationToken cancellationToken = default)
    {
        AgentGuard.RequireWorktree(ctx);
        var commands = args.OptAll("--cmd");
        if (commands.Count == 0)
            throw new CliUsageException("jz agent verify requires at least one --cmd.");

        await EnsureVerifyingAsync(client, ctx, cancellationToken);

        await client.RecordAgentEvidenceAsync(ctx.TaskId, "agent.verify.attempted",
            "Agent verification run started.",
            new { commands = commands.ToList() });

        var runs = await VerificationRunner.RunCommandsAsync(ctx.WorktreePath, commands, cancellationToken);
        var allPassed = runs.All(r => r.Passed);
        var last = new LastVerificationState
        {
            At = DateTimeOffset.UtcNow,
            AllPassed = allPassed,
            Commands = runs.Select(r => r.Command).ToList(),
        };

        RefreshContextFile(ctx, args.BaseUrl, commands.Select(c => c), last);

        var report = VerificationRunner.BuildReport(ctx.TaskId, ctx.SessionId, runs, readyForHumanReview: allPassed);

        if (!allPassed)
        {
            await client.RecordAgentEvidenceAsync(ctx.TaskId, "agent.verify.failed",
                "Local verification commands failed.",
                new { failed = runs.Where(r => !r.Passed).Select(r => r.Command).ToList() });

            var failedApi = await client.SubmitVerificationAsync(ctx.TaskId, report, supersede: false);
            return (failedApi, runs, false);
        }

        await client.RecordAgentEvidenceAsync(ctx.TaskId, "agent.verify.passed",
            "Local verification commands passed; call jz agent done to request human review.");

        return (CliHttpResult.FromResponse(System.Net.HttpStatusCode.OK,
            """{"ok":true,"message":"Verification passed locally; run jz agent done to submit for human review."}"""),
            runs, true);
    }

    public static async Task<CliHttpResult> BlockedAsync(
        JoyZoningCliClient client,
        CliArgs args,
        JoyZoningRuntimeContext ctx,
        CancellationToken cancellationToken = default)
    {
        AgentGuard.RequireWorktree(ctx);
        var reason = args.Opt("--reason") ?? throw new CliUsageException("--reason is required for agent blocked.");
        await client.RecordAgentEvidenceAsync(ctx.TaskId, "agent.blocked",
            "Agent reported blocked.", new { reason });
        return await client.AgentLeaseStatusAsync(ctx.TaskId, ExecutionLeaseStatus.Blocked, reason);
    }

    public static async Task<CliHttpResult> DoneAsync(
        JoyZoningCliClient client,
        CliArgs args,
        JoyZoningRuntimeContext ctx,
        CancellationToken cancellationToken = default)
    {
        AgentGuard.RequireWorktree(ctx);

        await client.RecordAgentEvidenceAsync(ctx.TaskId, "agent.done.requested",
            "Agent requested ready-for-human-review (not Complete).");

        var leaseCheck = await client.GetLeaseAsync(ctx.TaskId);
        if (leaseCheck.IsSuccess && leaseCheck.Body is not null)
        {
            var st = leaseCheck.Body.Value.GetProperty("status").GetInt32();
            if (st == (int)ExecutionLeaseStatus.ReadyForReview)
                return leaseCheck;
        }

        var last = ctx.LastVerification ?? JoyZoningRuntimeContext.TryLoad(ctx.WorktreePath)?.LastVerification;
        if (last is null || !last.AllPassed)
            throw new CliUsageException(
                "agent done requires passing jz agent verify first. Complete means ready_for_review, not merged.");

        var commands = args.OptAll("--cmd");
        IReadOnlyList<CommandRunResult> runs;
        if (commands.Count > 0)
        {
            runs = await VerificationRunner.RunCommandsAsync(ctx.WorktreePath, commands, cancellationToken);
            if (!runs.All(r => r.Passed))
                throw new CliUsageException("agent done aborted: --cmd verification failed.");
        }
        else if (last.Commands.Count > 0)
        {
            runs = last.Commands.Select(c => new CommandRunResult(c, true, 0, "from prior agent verify")).ToList();
        }
        else
            throw new CliUsageException("No verification commands on record; run jz agent verify first.");

        await EnsureVerifyingAsync(client, ctx, cancellationToken);

        var report = VerificationRunner.BuildReport(ctx.TaskId, ctx.SessionId, runs, readyForHumanReview: true);
        var result = await client.SubmitVerificationAsync(ctx.TaskId, report, args.Has("--supersede"));

        if (result.IsSuccess && result.Body is not null)
        {
            var status = result.Body.Value.GetProperty("status").GetInt32();
            if (status != (int)ExecutionLeaseStatus.ReadyForReview)
                throw new CliUsageException(
                    "Verification did not move lease to ready_for_review. Human review not requested.");
        }

        return result;
    }

    private static async Task EnsureVerifyingAsync(
        JoyZoningCliClient client,
        JoyZoningRuntimeContext ctx,
        CancellationToken cancellationToken)
    {
        var lease = await client.GetLeaseAsync(ctx.TaskId);
        if (!lease.IsSuccess || lease.Body is null)
            throw new CliUsageException(lease.Message ?? "No active lease.");

        var status = lease.Body.Value.GetProperty("status").GetInt32();
        if (status == (int)ExecutionLeaseStatus.Verifying)
            return;

        if (status is (int)ExecutionLeaseStatus.Running or (int)ExecutionLeaseStatus.Blocked)
        {
            var transition = await client.AgentLeaseStatusAsync(
                ctx.TaskId, ExecutionLeaseStatus.Verifying, null);
            if (!transition.IsSuccess)
                throw new CliUsageException(transition.Message ?? "Could not enter verifying state.");
            return;
        }

        if (status == (int)ExecutionLeaseStatus.ReadyForReview)
            return;

        throw new CliUsageException(
            $"Lease status {(ExecutionLeaseStatus)status} cannot accept verification.");
    }

    private static void RefreshContextFile(
        JoyZoningRuntimeContext ctx,
        string baseUrl,
        IEnumerable<string>? verificationCommands,
        LastVerificationState? last)
    {
        var lease = MapLeaseElementForWrite(ctx);
        JoyZoningRuntimeContext.Write(lease, baseUrl, verificationCommands, last);
    }

    private static ExecutionLease MapLeaseElement(JsonElement el, Guid sessionId, string worktree) =>
        new()
        {
            Id = ReadGuid(el, "id") ?? Guid.Empty,
            WorkTaskId = ReadGuid(el, "workTaskId") ?? Guid.Empty,
            AssignedSessionId = sessionId,
            OperatorSessionId = sessionId,
            WorktreePath = worktree,
            Status = (ExecutionLeaseStatus)el.GetProperty("status").GetInt32(),
        };

    private static ExecutionLease MapLeaseElementForWrite(JoyZoningRuntimeContext ctx) =>
        new()
        {
            Id = ctx.LeaseId,
            WorkTaskId = ctx.TaskId,
            AssignedSessionId = ctx.SessionId,
            OperatorSessionId = ctx.SessionId,
            WorktreePath = ctx.WorktreePath,
            Status = Enum.TryParse<ExecutionLeaseStatus>(ctx.LeaseStatus, out var s)
                ? s
                : ExecutionLeaseStatus.Running,
        };

    private static Guid? ReadGuid(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && Guid.TryParse(p.GetString(), out var g) ? g : null;

    private static string? ReadString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
}
