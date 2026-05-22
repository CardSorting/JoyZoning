using System.Text.Json;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;

namespace JoyZoning.Cli;

public static class OperatorWorkflows
{
    public static async Task<CliHttpResult> RunTaskAsync(
        JoyZoningCliClient client,
        CliArgs args,
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        var dispatch = await client.DispatchTaskAsync(taskId, args.ApproveCritical);
        if (!dispatch.IsSuccess)
            return dispatch;

        var pollSeconds = int.TryParse(args.Opt("--poll"), out var ps) ? ps : 0;
        var timeoutSeconds = int.TryParse(args.Opt("--timeout"), out var ts) ? ts : 600;

        if (pollSeconds <= 0)
            return await client.GetLeaseAsync(taskId);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(timeoutSeconds);
        CliHttpResult? last = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            last = await client.GetLeaseAsync(taskId);
            if (!last.IsSuccess)
                return last;

            var status = last.Body!.Value.GetProperty("status").GetInt32();
            if (status is (int)ExecutionLeaseStatus.Blocked
                or (int)ExecutionLeaseStatus.Revoked
                or (int)ExecutionLeaseStatus.ReadyForReview
                or (int)ExecutionLeaseStatus.Merged)
                return last;

            await Task.Delay(TimeSpan.FromSeconds(pollSeconds), cancellationToken);
        }

        return last ?? await client.GetLeaseAsync(taskId);
    }

    public static async Task<(CliHttpResult Result, IReadOnlyList<CommandRunResult> Runs)> VerifyTaskAsync(
        JoyZoningCliClient client,
        CliArgs args,
        Guid? explicitTaskId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await LeaseContextResolver.ResolveAsync(client, args, explicitTaskId, cancellationToken);
        var commands = args.OptAll("--cmd");
        var workDir = args.Opt("--workdir") ?? ctx.WorktreePath;
        if (string.IsNullOrWhiteSpace(workDir))
            throw new CliUsageException("No worktree path on lease; pass --workdir.");

        var runs = await VerificationRunner.RunCommandsAsync(workDir, commands, cancellationToken);
        var report = VerificationRunner.BuildReport(ctx.TaskId, ctx.SessionId, runs);
        var supersede = args.Has("--supersede");
        var api = await client.SubmitVerificationAsync(ctx.TaskId, report, supersede);

        if (!runs.All(r => r.Passed))
        {
            if (api.IsSuccess)
            {
                return (api, runs);
            }
        }

        return (api, runs);
    }

    public static Task<CliHttpResult> CompleteTaskAsync(
        JoyZoningCliClient client,
        CliArgs args,
        Guid taskId) =>
        client.MergeLeaseAsync(taskId);

    public static async Task<CliHttpResult> FailTaskAsync(
        JoyZoningCliClient client,
        CliArgs args,
        Guid taskId)
    {
        var reason = args.Opt("--reason") ?? throw new CliUsageException("--reason is required for task fail.");
        return await client.FailLeaseAsync(taskId, reason);
    }

    public static LeaseRecoveryMode ParseRecoveryMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
            throw new CliUsageException("--mode is required (reopen|reattach|replace or 0|1|2).");

        return mode.Trim().ToLowerInvariant() switch
        {
            "0" or "reopen" or "reopenblocked" => LeaseRecoveryMode.ReopenBlocked,
            "1" or "reattach" or "reattachworktree" => LeaseRecoveryMode.ReattachWorktree,
            "2" or "replace" or "replacement" or "replacementlease" => LeaseRecoveryMode.ReplacementLease,
            _ => throw new CliUsageException($"Unknown recovery mode: {mode}"),
        };
    }

    public static async Task<CliHttpResult> RecoverTaskAsync(
        JoyZoningCliClient client,
        CliArgs args,
        Guid taskId)
    {
        var mode = ParseRecoveryMode(args.Opt("--mode"));
        if (mode == LeaseRecoveryMode.ReplacementLease)
            CliSafety.RequireYes(args, "recover with replacement lease");

        var sessionId = args.RequireSessionId(CliArgs.ParseGuidOpt(args.Raw, "--session"));
        return await client.RecoverLeaseAsync(
            taskId,
            sessionId,
            mode,
            StatusChangeActor.Human,
            args.ApproveCritical);
    }

}
