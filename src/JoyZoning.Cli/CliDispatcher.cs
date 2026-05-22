using System.Net.Http;
using System.Text.Json;
using JoyZoning.Cli.Tui;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;

namespace JoyZoning.Cli;

public static class CliDispatcher
{
    public static async Task<int> DispatchAsync(JoyZoningCliClient client, CliContext ctx)
    {
        var a = ctx.Args.Positionals;
        if (a.Length == 0)
            throw new CliUsageException("Missing command.");

        var cmd = a[0].ToLowerInvariant();
        var raw = ctx.Args.Raw;

        return cmd switch
        {
            "health" => CliOutput.WriteResult(ctx, await client.HealthAsync()),
            "doctor" => await DoctorCommand.RunAsync(ctx),
            "lease" => await DispatchLeaseShortcutAsync(client, ctx, a),
            "heartbeat" => await DispatchHeartbeatShortcutAsync(client, ctx, a),
            "verify" => await DispatchVerifyShortcutAsync(client, ctx, a),
            "session" => CliOutput.WriteResult(ctx, await DispatchSessionAsync(client, ctx, a)),
            "task" => await DispatchTaskAsync(client, ctx, a),
            "execution" => CliOutput.WriteResult(ctx, await DispatchExecutionAsync(client, ctx, a)),
            "approval" => CliOutput.WriteResult(ctx, await DispatchApprovalAsync(client, ctx, a)),
            "manager" => CliOutput.WriteResult(ctx, await DispatchManagerAsync(client, ctx, a)),
            "event" or "events" => CliOutput.WriteResult(ctx, await DispatchEventAsync(client, ctx, a)),
            "hermes" => CliOutput.WriteResult(ctx, await DispatchHermesAsync(client, ctx, a)),
            "workspace" => CliOutput.WriteResult(ctx, await DispatchWorkspaceAsync(client, ctx, a)),
            "config" => await DispatchConfigAsync(client, ctx, a),
            "kanban" => CliOutput.WriteResult(ctx, await DispatchKanbanAsync(client, ctx, a)),
            "tui" => await OperatorTuiRunner.RunAsync(ctx),
            "completion" => DispatchCompletion(ctx, a),
            "agent" => await DispatchAgentAsync(client, ctx, a),
            "yolo" => await YoloCommand.DispatchAsync(client, ctx, a),
            "raw" => CliOutput.WriteResult(ctx, await DispatchRawAsync(client, ctx, a)),
            _ => throw new CliUsageException($"Unknown command: {cmd}. Run jz --help."),
        };
    }

    private static async Task<int> DispatchLeaseShortcutAsync(
        JoyZoningCliClient client, CliContext ctx, string[] a)
    {
        var taskId = ParseOptionalTaskId(a, 1);
        var resolved = await LeaseContextResolver.ResolveAsync(client, ctx.Args, taskId);
        return CliOutput.WriteResult(ctx, await client.GetLeaseAsync(resolved.TaskId));
    }

    private static async Task<int> DispatchHeartbeatShortcutAsync(
        JoyZoningCliClient client, CliContext ctx, string[] a)
    {
        var resolved = await LeaseContextResolver.ResolveAsync(client, ctx.Args, ParseOptionalTaskId(a, 1));
        return CliOutput.WriteResult(ctx, await client.HeartbeatLeaseAsync(
            resolved.TaskId,
            resolved.SessionId,
            (StatusChangeActor)CliArgs.ParseIntOpt(ctx.Args.Raw, "--actor", (int)StatusChangeActor.DietCode)));
    }

    private static async Task<int> DispatchVerifyShortcutAsync(
        JoyZoningCliClient client, CliContext ctx, string[] a) =>
        await RunVerifyAsync(client, ctx, ParseOptionalTaskId(a, 1));

    private static Guid? ParseOptionalTaskId(string[] a, int index)
    {
        if (a.Length <= index)
            return null;
        if (!Guid.TryParse(a[index], out var id))
            throw new CliUsageException("Task id must be a GUID.");
        return id;
    }

    private static async Task<int> DispatchAgentAsync(
        JoyZoningCliClient client, CliContext ctx, string[] a)
    {
        RequireArgs(a, 2, "agent <start|heartbeat|verify|blocked|done>");
        var sub = a[1].ToLowerInvariant();
        var args = ctx.Args;

        switch (sub)
        {
            case "start":
            {
                Guid? taskId = CliArgs.ParseGuidOpt(args.Raw, "--task");
                if (!taskId.HasValue && a.Length > 2 && Guid.TryParse(a[2], out var positional))
                    taskId = positional;
                if (!taskId.HasValue)
                    throw new CliUsageException("--task <id> is required for agent start.");
                var result = await AgentHarness.StartAsync(client, args, taskId.Value);
                return CliOutput.WriteResult(ctx, result);
            }
            case "heartbeat":
            {
                var fileCtx = JoyZoningRuntimeContext.TryLoad();
                AgentGuard.RequireRuntimeContext(fileCtx);
                var result = await AgentHarness.HeartbeatAsync(client, fileCtx!);
                return CliOutput.WriteResult(ctx, result);
            }
            case "verify":
            {
                var fileCtx = JoyZoningRuntimeContext.TryLoad();
                AgentGuard.RequireRuntimeContext(fileCtx);
                var (api, runs, passed) = await AgentHarness.VerifyAsync(client, args, fileCtx!);
                var code = CliOutput.WriteResult(ctx, api);
                if (!passed)
                    return code == 0 ? 1 : code;
                return code;
            }
            case "blocked":
            {
                var fileCtx = JoyZoningRuntimeContext.TryLoad();
                AgentGuard.RequireRuntimeContext(fileCtx);
                return CliOutput.WriteResult(ctx, await AgentHarness.BlockedAsync(client, args, fileCtx!));
            }
            case "done":
            {
                var fileCtx = JoyZoningRuntimeContext.TryLoad();
                AgentGuard.RequireRuntimeContext(fileCtx);
                return CliOutput.WriteResult(ctx, await AgentHarness.DoneAsync(client, args, fileCtx!));
            }
            default:
                throw Usage("agent start | heartbeat | verify | blocked | done");
        }
    }

    private static async Task<int> DispatchTaskAsync(
        JoyZoningCliClient client, CliContext ctx, string[] a)
    {
        RequireArgs(a, 2, "task <subcommand>");
        var sub = a[1].ToLowerInvariant();
        var args = ctx.Args;
        var raw = args.Raw;

        if (args.AgentMode)
        {
            AgentGuard.RejectForbiddenSubcommand(sub);
            if (sub is "dispatch" or "run" or "dispatch-retry")
                throw new CliUsageException("Agent harness cannot dispatch; human operator must dispatch first.");
        }

        switch (sub)
        {
            case "run":
                var runId = CliArgs.RequireGuid(a, 2, "task id");
                return CliOutput.WriteResult(ctx, await OperatorWorkflows.RunTaskAsync(client, args, runId));

            case "verify":
                return await RunVerifyAsync(client, ctx, CliArgs.RequireGuid(a, 2, "task id"));

            case "complete":
                CliSafety.RequireYes(args, "task complete (human merge)");
                return CliOutput.WriteResult(ctx,
                    await OperatorWorkflows.CompleteTaskAsync(client, args, CliArgs.RequireGuid(a, 2, "task id")));

            case "fail":
                return CliOutput.WriteResult(ctx,
                    await OperatorWorkflows.FailTaskAsync(client, args, CliArgs.RequireGuid(a, 2, "task id")));

            case "recover":
                return CliOutput.WriteResult(ctx,
                    await OperatorWorkflows.RecoverTaskAsync(client, args, CliArgs.RequireGuid(a, 2, "task id")));

            case "list":
                return CliOutput.WriteResult(ctx, await client.ListTasksAsync(
                    args.RequireSessionId(CliArgs.ParseGuidOpt(raw, "--session"))));

            case "create":
                return CliOutput.WriteResult(ctx, await client.CreateTaskAsync(
                    args.RequireSessionId(CliArgs.ParseGuidOpt(raw, "--session")),
                    CliArgs.OptStatic(raw, "--title") ?? throw Usage("--title required"),
                    CliArgs.OptStatic(raw, "--description"),
                    (AgentKind)CliArgs.ParseIntOpt(raw, "--agent", (int)AgentKind.DietCode),
                    (RiskLevel)CliArgs.ParseIntOpt(raw, "--risk", (int)RiskLevel.Low)));

            case "status":
                var status = (WorkTaskStatus)CliArgs.RequireInt(raw, "--status");
                CliSafety.ForbidDirectComplete(status);
                AgentGuard.RejectCompleteStatus(status);
                return CliOutput.WriteResult(ctx, await client.UpdateTaskStatusAsync(
                    CliArgs.RequireGuid(a, 2, "task id"),
                    status,
                    (StatusChangeActor)CliArgs.ParseIntOpt(raw, "--actor", (int)StatusChangeActor.Human)));

            case "dispatch":
                return CliOutput.WriteResult(ctx,
                    await client.DispatchTaskAsync(
                        CliArgs.RequireGuid(a, 2, "task id"),
                        args.ApproveCritical));

            case "lease":
                return CliOutput.WriteResult(ctx,
                    await client.GetLeaseAsync(CliArgs.RequireGuid(a, 2, "task id")));

            case "heartbeat":
                var hbId = CliArgs.RequireGuid(a, 2, "task id");
                return CliOutput.WriteResult(ctx, await client.HeartbeatLeaseAsync(
                    hbId,
                    args.RequireSessionId(CliArgs.ParseGuidOpt(raw, "--session")),
                    (StatusChangeActor)CliArgs.ParseIntOpt(raw, "--actor", (int)StatusChangeActor.DietCode)));

            case "dispatch-retry":
                return CliOutput.WriteResult(ctx, await client.DispatchRetryAsync(
                    CliArgs.RequireGuid(a, 2, "task id"),
                    args.RequireSessionId(CliArgs.ParseGuidOpt(raw, "--session")),
                    (StatusChangeActor)CliArgs.ParseIntOpt(raw, "--actor", (int)StatusChangeActor.Human),
                    args.ApproveCritical));

            case "agent-status":
                return CliOutput.WriteResult(ctx, await client.AgentLeaseStatusAsync(
                    CliArgs.RequireGuid(a, 2, "task id"),
                    (ExecutionLeaseStatus)CliArgs.RequireInt(raw, "--status"),
                    CliArgs.OptStatic(raw, "--reason")));

            case "verification":
                return CliOutput.WriteResult(ctx, await client.SubmitVerificationAsync(
                    CliArgs.RequireGuid(a, 2, "task id"),
                    LoadVerificationReport(CliArgs.OptStatic(raw, "--file") ?? throw Usage("--file required")),
                    args.Has("--supersede")));

            case "revoke":
                CliSafety.RequireYes(args, "task revoke");
                return CliOutput.WriteResult(ctx, await client.RevokeLeaseAsync(
                    CliArgs.RequireGuid(a, 2, "task id"),
                    CliArgs.OptStatic(raw, "--reason")));

            case "merge":
                CliSafety.RequireYes(args, "task merge");
                return CliOutput.WriteResult(ctx,
                    await client.MergeLeaseAsync(CliArgs.RequireGuid(a, 2, "task id")));

            case "import-kanban":
                return CliOutput.WriteResult(ctx, await client.ImportKanbanAsync(
                    args.RequireSessionId(CliArgs.ParseGuidOpt(raw, "--session"))));

            default:
                throw Usage("task run | verify | complete | fail | recover | list | create | ...");
        }
    }

    private static async Task<int> RunVerifyAsync(JoyZoningCliClient client, CliContext ctx, Guid? taskId)
    {
        if (ctx.Args.OptAll("--cmd").Count == 0 && ctx.Args.Opt("--file") is null)
            throw new CliUsageException("task verify requires --cmd or --file.");

        if (ctx.Args.Opt("--file") is { } file)
        {
            var id = taskId ?? throw new CliUsageException("task id required with --file.");
            return CliOutput.WriteResult(ctx, await client.SubmitVerificationAsync(
                id, LoadVerificationReport(file), ctx.Args.Has("--supersede")));
        }

        var (result, runs) = await OperatorWorkflows.VerifyTaskAsync(client, ctx.Args, taskId);
        var code = CliOutput.WriteResult(ctx, result);
        if (code != 0)
            return code;
        if (!runs.All(r => r.Passed))
        {
            if (!ctx.Quiet)
            {
                var fail = new { ok = false, error = "verification_failed", commands = runs };
                CliOutput.WriteEnvelope(ctx, fail);
            }

            return 1;
        }

        return 0;
    }

    private static async Task<CliHttpResult> DispatchSessionAsync(
        JoyZoningCliClient client, CliContext ctx, string[] a)
    {
        RequireArgs(a, 2, "session <list|get|create>");
        var raw = ctx.Args.Raw;
        return a[1].ToLowerInvariant() switch
        {
            "list" => await client.ListSessionsAsync(),
            "get" => await client.GetSessionAsync(CliArgs.RequireGuid(a, 2, "session id")),
            "create" => await client.CreateSessionAsync(
                CliArgs.OptStatic(raw, "--name") ?? throw Usage("--name required"),
                CliArgs.OptStatic(raw, "--workspace") ?? throw Usage("--workspace required"),
                CliArgs.OptStatic(raw, "--profile")),
            _ => throw Usage("session list | get | create"),
        };
    }

    private static async Task<CliHttpResult> DispatchExecutionAsync(
        JoyZoningCliClient client, CliContext ctx, string[] a)
    {
        RequireArgs(a, 2, "execution <subcommand>");
        return a[1].ToLowerInvariant() switch
        {
            "get" => await client.GetExecutionAsync(CliArgs.RequireGuid(a, 2, "execution id")),
            "interrupted" => await client.ListInterruptedExecutionsAsync(),
            "resume" => await client.ResumeExecutionAsync(CliArgs.RequireGuid(a, 2, "execution id")),
            "cancel" => await client.CancelExecutionAsync(CliArgs.RequireGuid(a, 2, "execution id")),
            _ => throw Usage("execution get | interrupted | resume | cancel"),
        };
    }

    private static async Task<CliHttpResult> DispatchApprovalAsync(
        JoyZoningCliClient client, CliContext ctx, string[] a)
    {
        RequireArgs(a, 2, "approval <list|resolve>");
        return a[1].ToLowerInvariant() switch
        {
            "list" => await client.ListPendingApprovalsAsync(),
            "resolve" => await client.ResolveApprovalAsync(
                CliArgs.RequireGuid(a, 2, "approval id"),
                (ApprovalScope)CliArgs.RequireInt(ctx.Args.Raw, "--scope")),
            _ => throw Usage("approval list | resolve"),
        };
    }

    private static async Task<CliHttpResult> DispatchManagerAsync(
        JoyZoningCliClient client, CliContext ctx, string[] a)
    {
        RequireArgs(a, 2, "manager message");
        if (a[1] != "message")
            throw Usage("manager message --text <msg>");
        return await client.SendManagerMessageAsync(
            ctx.Args.RequireSessionId(CliArgs.ParseGuidOpt(ctx.Args.Raw, "--session")),
            CliArgs.OptStatic(ctx.Args.Raw, "--text") ?? throw Usage("--text required"));
    }

    private static async Task<CliHttpResult> DispatchEventAsync(
        JoyZoningCliClient client, CliContext ctx, string[] a)
    {
        RequireArgs(a, 2, "event list");
        if (a[1] != "list")
            throw Usage("event list");
        long? since = long.TryParse(CliArgs.OptStatic(ctx.Args.Raw, "--since"), out var s) ? s : null;
        return await client.ListEventsAsync(since, CliArgs.ParseGuidOpt(ctx.Args.Raw, "--correlation"),
            CliArgs.OptStatic(ctx.Args.Raw, "--types"));
    }

    private static async Task<CliHttpResult> DispatchHermesAsync(
        JoyZoningCliClient client, CliContext ctx, string[] a)
    {
        RequireArgs(a, 2, "hermes <subcommand>");
        var sub = a[1].ToLowerInvariant();
        if (sub == "tui")
        {
            var (installRoot, profile) = await HermesTuiLauncher.ResolveHermesConfigAsync(client);
            var resume = ctx.Args.Has("-c") || ctx.Args.Has("--continue");
            var code = await HermesTuiLauncher.LaunchAsync(installRoot, profile, resume);
            if (code != 0)
                throw new CliUsageException($"Hermes TUI exited with code {code}.");
            return new CliHttpResult(
                System.Net.HttpStatusCode.OK,
                """{"ok":true,"message":"Hermes TUI session ended."}""",
                JsonSerializer.Deserialize<JsonElement>("""{"ok":true}""", JoyZoningCliClient.JsonOptions),
                null,
                null,
                false);
        }

        return sub switch
        {
            "health" => await client.HermesHealthAsync(),
            "ensure" => await client.HermesEnsureAsync(),
            "dashboard" => await client.HermesDashboardAsync(),
            "ensure-dashboard" => await client.HermesEnsureDashboardAsync(!ctx.Args.Has("--no-gateway")),
            _ => throw Usage("hermes health | ensure | dashboard | ensure-dashboard | tui"),
        };
    }

    private static async Task<CliHttpResult> DispatchWorkspaceAsync(
        JoyZoningCliClient client, CliContext ctx, string[] a)
    {
        RequireArgs(a, 2, "workspace <tree|changed|diff>");
        var root = CliArgs.OptStatic(ctx.Args.Raw, "--root") ?? throw Usage("--root required");
        return a[1].ToLowerInvariant() switch
        {
            "tree" => await client.WorkspaceTreeAsync(root),
            "changed" => await client.WorkspaceChangedAsync(root),
            "diff" => await client.WorkspaceDiffAsync(root,
                CliArgs.OptStatic(ctx.Args.Raw, "--path") ?? throw Usage("--path required")),
            _ => throw Usage("workspace tree | changed | diff"),
        };
    }

    private static async Task<int> DispatchConfigAsync(
        JoyZoningCliClient client, CliContext ctx, string[] a)
    {
        RequireArgs(a, 2, "config <get|explain>");
        return a[1].ToLowerInvariant() switch
        {
            "get" => CliOutput.WriteResult(ctx, await client.GetConfigAsync()),
            "explain" => ConfigExplainCommand.Run(ctx),
            _ => throw Usage("config get | explain"),
        };
    }

    private static async Task<CliHttpResult> DispatchKanbanAsync(
        JoyZoningCliClient client, CliContext ctx, string[] a)
    {
        if (a is not ["kanban", "sync-status"])
            throw Usage("kanban sync-status");
        return await client.KanbanSyncStatusAsync();
    }

    private static int DispatchCompletion(CliContext ctx, string[] a)
    {
        RequireArgs(a, 2, "completion bash");
        if (a[1] != "bash")
            throw Usage("completion bash");
        Console.Out.WriteLine(CompletionScript.Bash);
        return 0;
    }

    private static async Task<CliHttpResult> DispatchRawAsync(
        JoyZoningCliClient client, CliContext ctx, string[] a)
    {
        RequireArgs(a, 3, "raw <METHOD> <path>");
        var method = a[1];
        var path = a[2];
        if (ctx.Args.AgentMode)
            throw new CliUsageException("Agent harness cannot use raw API. Use jz agent * commands.");
        CliSafety.GuardRawRequest(ctx.Args, method, path);

        string? body = null;
        var bodyArg = CliArgs.OptStatic(ctx.Args.Raw, "--body");
        if (bodyArg == "@stdin")
            body = await Console.In.ReadToEndAsync();
        else if (bodyArg is not null)
            body = await File.ReadAllTextAsync(bodyArg);

        return await client.RawAsync(new HttpMethod(method.ToUpperInvariant()), path, body);
    }

    private static VerificationReport LoadVerificationReport(string path)
    {
        var json = File.ReadAllText(path);
        var report = JsonSerializer.Deserialize<VerificationReportDto>(json, JoyZoningCliClient.JsonOptions)
            ?? throw new CliUsageException($"Could not parse verification report: {path}");

        return new VerificationReport
        {
            CardId = report.CardId,
            SessionId = report.SessionId,
            ChangedFiles = report.ChangedFiles is { Count: > 0 } cf ? cf : [],
            CommandsRun = (report.CommandsRun ?? [])
                .Select(c => new CommandRunSummary
                {
                    Command = c.Command ?? "",
                    Passed = c.Passed,
                    Summary = c.Summary ?? "",
                })
                .ToList(),
            Risks = report.Risks is { Count: > 0 } r ? r : [],
            UnresolvedQuestions = report.UnresolvedQuestions is { Count: > 0 } u ? u : [],
            ReadyForHumanReview = report.ReadyForHumanReview,
        };
    }

    private sealed class VerificationReportDto
    {
        public Guid CardId { get; set; }
        public Guid SessionId { get; set; }
        public List<string>? ChangedFiles { get; set; }
        public List<CommandRunSummaryDto>? CommandsRun { get; set; }
        public List<string>? Risks { get; set; }
        public List<string>? UnresolvedQuestions { get; set; }
        public bool ReadyForHumanReview { get; set; }
    }

    private sealed class CommandRunSummaryDto
    {
        public string? Command { get; set; }
        public bool Passed { get; set; }
        public string? Summary { get; set; }
    }

    private static void RequireArgs(string[] a, int min, string hint)
    {
        if (a.Length < min)
            throw new CliUsageException(hint);
    }

    private static CliUsageException Usage(string hint) => new(hint);
}
