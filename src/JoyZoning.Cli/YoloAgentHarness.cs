using System.Text.Json;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;

namespace JoyZoning.Cli;

/// <summary>Agent harness steps constrained to YOLO-allowed APIs.</summary>
public static class YoloAgentHarness
{
    public static async Task<CliHttpResult> StartAsync(
        IYoloRunClient client,
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

        var entity = new ExecutionLease
        {
            Id = ReadGuid(lease, "id") ?? Guid.Empty,
            WorkTaskId = taskId,
            AssignedSessionId = sessionId,
            OperatorSessionId = sessionId,
            WorktreePath = worktree,
            Status = (ExecutionLeaseStatus)lease.GetProperty("status").GetInt32(),
        };
        JoyZoningRuntimeContext.Write(entity, args.BaseUrl);

        await client.RecordAgentEvidenceAsync(taskId, "agent.started",
            "YOLO agent harness bound to lease worktree.",
            new { worktree, contextFile = JoyZoningRuntimeContext.RelativePath });

        return leaseResult;
    }

    public static async Task<(CliHttpResult Api, IReadOnlyList<CommandRunResult> Runs, bool AllPassed)> VerifyAsync(
        IYoloRunClient client,
        CliArgs args,
        JoyZoningRuntimeContext ctx,
        YoloPolicy policy,
        IReadOnlyList<string> verificationCommands,
        CancellationToken cancellationToken = default)
    {
        var commands = verificationCommands.Count > 0
            ? verificationCommands
            : args.OptAll("--cmd");
        if (commands.Count == 0)
            throw new CliUsageException("YOLO verify requires commands from policy.");

        foreach (var cmd in commands)
            policy.AssertCommandAllowed(cmd);

        await EnsureVerifyingAsync(client, ctx, cancellationToken);

        await client.RecordAgentEvidenceAsync(ctx.TaskId, "agent.verify.attempted",
            "YOLO verification run started.",
            new { commands = commands.ToList() });

        var runs = await VerificationRunner.RunCommandsAsync(ctx.WorktreePath, commands, cancellationToken);
        var allPassed = runs.All(r => r.Passed);

        var last = new LastVerificationState
        {
            At = DateTimeOffset.UtcNow,
            AllPassed = allPassed,
            Commands = runs.Select(r => r.Command).ToList(),
        };
        RefreshContextFile(ctx, args.BaseUrl, commands, last);

        var report = VerificationRunner.BuildReport(ctx.TaskId, ctx.SessionId, runs, readyForHumanReview: allPassed);

        if (!allPassed)
        {
            await client.RecordAgentEvidenceAsync(ctx.TaskId, "agent.verify.failed",
                "YOLO local verification commands failed.",
                new { failed = runs.Where(r => !r.Passed).Select(r => r.Command).ToList() });
            var failedApi = await client.SubmitVerificationAsync(ctx.TaskId, report, supersede: false);
            return (failedApi, runs, false);
        }

        await client.RecordAgentEvidenceAsync(ctx.TaskId, "agent.verify.passed",
            "YOLO local verification passed; submitting for human review next.");

        return (CliHttpResult.FromResponse(System.Net.HttpStatusCode.OK,
            """{"ok":true,"message":"Verification passed locally."}"""), runs, true);
    }

    public static async Task<CliHttpResult> DoneAsync(
        IYoloRunClient client,
        CliArgs args,
        JoyZoningRuntimeContext ctx,
        CancellationToken cancellationToken = default)
    {
        await client.RecordAgentEvidenceAsync(ctx.TaskId, "agent.done.requested",
            "YOLO requested ready-for-human-review (not Complete).");

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
                "YOLO done requires passing verification first.");

        var runs = last.Commands.Select(c => new CommandRunResult(c, true, 0, "from prior YOLO verify")).ToList();

        await EnsureVerifyingAsync(client, ctx, cancellationToken);

        var report = VerificationRunner.BuildReport(ctx.TaskId, ctx.SessionId, runs, readyForHumanReview: true);
        var result = await client.SubmitVerificationAsync(ctx.TaskId, report, args.Has("--supersede"));

        if (result.IsSuccess && result.Body is not null)
        {
            var status = result.Body.Value.GetProperty("status").GetInt32();
            if (status != (int)ExecutionLeaseStatus.ReadyForReview)
                throw new CliUsageException(
                    "Verification did not move lease to ready_for_review.");
        }

        return result;
    }

    private static async Task EnsureVerifyingAsync(
        IYoloRunClient client,
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
            var transition = await client.AgentLeaseStatusAsync(ctx.TaskId, ExecutionLeaseStatus.Verifying, null);
            if (!transition.IsSuccess)
                throw new CliUsageException(transition.Message ?? "Could not enter verifying state.");
            return;
        }

        if (status == (int)ExecutionLeaseStatus.ReadyForReview)
            return;

        throw new CliUsageException($"Lease status {(ExecutionLeaseStatus)status} cannot accept verification.");
    }

    private static void RefreshContextFile(
        JoyZoningRuntimeContext ctx,
        string baseUrl,
        IEnumerable<string> verificationCommands,
        LastVerificationState last)
    {
        var lease = new ExecutionLease
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
        JoyZoningRuntimeContext.Write(lease, baseUrl, verificationCommands, last);
    }

    private static Guid? ReadGuid(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && Guid.TryParse(p.GetString(), out var g) ? g : null;

    private static string? ReadString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
}
