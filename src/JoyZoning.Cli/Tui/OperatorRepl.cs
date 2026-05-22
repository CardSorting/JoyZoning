using System.Text;
using System.Text.Json;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Cli.Tui;

public sealed class OperatorRepl
{
    private readonly JoyZoningCliClient _client;
    private readonly CliContext _ctx;
    private readonly TerminalLineReader _reader;
    private OperatorHubStream? _hub;
    private CancellationTokenSource? _activityCts;
    private bool _prettyJson;
    private Guid? _sessionId;
    private Guid? _taskId;
    private readonly StringBuilder _managerBuffer = new();
    private readonly object _streamLock = new();

    public OperatorRepl(JoyZoningCliClient client, CliContext ctx)
    {
        _client = client;
        _ctx = ctx;
        _prettyJson = ctx.PrettyJson;
        _sessionId = ctx.Args.SessionId;
        _taskId = ctx.Args.TaskId;
        _reader = new TerminalLineReader(CompleteInput);
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        PrintBanner();
        await EnsureHubAsync(cancellationToken);

        using var globalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (_, e) =>
        {
            if (_activityCts is { IsCancellationRequested: false })
            {
                e.Cancel = true;
                _activityCts.Cancel();
                WriteActivityLine("interrupt", "Stopped watch / poll / manager stream.");
                return;
            }

            e.Cancel = true;
            globalCts.Cancel();
        };

        while (!globalCts.Token.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = _reader.ReadLine(BuildPrompt(), globalCts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (line is null)
                break;

            if (line.Length == 0)
                continue;

            try
            {
                if (OperatorSlashRegistry.TryResolve(line, out var cmd, out var args))
                    await ExecuteSlashAsync(cmd, args, globalCts.Token);
                else
                    await SendManagerShortcutAsync(line, globalCts.Token);
            }
            catch (CliUsageException ex)
            {
                WriteActivityLine("error", ex.Message);
            }
            catch (Exception ex)
            {
                WriteActivityLine("error", ex.Message);
            }
        }

        _activityCts?.Cancel();
        if (_hub is not null)
            await _hub.DisposeAsync();
        Console.WriteLine();
        return 0;
    }

    private string BuildPrompt()
    {
        var parts = new List<string> { "jz" };
        if (_sessionId.HasValue)
            parts.Add($"s:{ShortId(_sessionId.Value)}");
        if (_taskId.HasValue)
            parts.Add($"t:{ShortId(_taskId.Value)}");
        return string.Join("·", parts) + "› ";
    }

    private static string ShortId(Guid id) => id.ToString()[..8];

    private IReadOnlyList<string> CompleteInput(string current)
    {
        if (current.StartsWith('/'))
            return OperatorSlashRegistry.CompletionCandidates(current);

        return [];
    }

    private async Task ExecuteSlashAsync(string cmd, string args, CancellationToken cancellationToken)
    {
        switch (cmd)
        {
            case "help":
                OperatorSlashRegistry.PrintHelp();
                return;
            case "quit":
                throw new OperationCanceledException();
            case "doctor":
                await RunDoctorAsync();
                return;
            case "clear":
                Console.Clear();
                PrintBanner();
                return;
            case "json":
                _prettyJson = !_prettyJson;
                WriteActivityLine("info", _prettyJson ? "Pretty JSON on." : "Pretty JSON off.");
                return;
            case "session":
                await RunSessionAsync(args, cancellationToken);
                return;
            case "tasks":
                await RunTasksAsync(cancellationToken);
                return;
            case "use":
                RunUse(args);
                return;
            case "dispatch":
            case "run":
                await RunDispatchAsync(args, cancellationToken);
                return;
            case "lease":
                await RunLeaseAsync(cancellationToken);
                return;
            case "heartbeat":
                await RunHeartbeatAsync(cancellationToken);
                return;
            case "verify":
                await RunVerifyAsync(args, cancellationToken);
                return;
            case "complete":
                await RunCompleteAsync(args, cancellationToken);
                return;
            case "events":
                await RunEventsAsync(args, cancellationToken);
                return;
            case "approvals":
                await RunApprovalsAsync(cancellationToken);
                return;
            case "manager":
                await RunManagerAsync(args, cancellationToken);
                return;
            case "watch":
                await RunWatchAsync(cancellationToken);
                return;
            case "stop":
                _activityCts?.Cancel();
                WriteActivityLine("info", "Stopped background activity.");
                return;
            case "hermes":
                await RunHermesTuiAsync(args, cancellationToken);
                return;
            default:
                WriteActivityLine("error", $"Unknown command /{cmd}");
                return;
        }
    }

    private async Task RunDoctorAsync()
    {
        var doctorCtx = new CliContext
        {
            Args = new CliArgs
            {
                Raw = _ctx.Args.Raw,
                Positionals = _ctx.Args.Positionals,
                BaseUrl = _ctx.Args.BaseUrl,
                SessionId = _sessionId ?? _ctx.Args.SessionId,
                TaskId = _taskId ?? _ctx.Args.TaskId,
                PrettyJson = _prettyJson,
                Quiet = false,
                Yes = _ctx.Args.Yes,
                FieldPath = _ctx.Args.FieldPath,
            },
        };
        await DoctorCommand.RunAsync(doctorCtx);
    }

    private async Task RunSessionAsync(string args, CancellationToken cancellationToken)
    {
        var parts = SplitArgs(args);
        if (parts.Length == 0 || parts[0] is "list")
        {
            var result = await _client.ListSessionsAsync();
            PrintResult(result);
            return;
        }

        if (parts[0] is "use" && parts.Length > 1 && Guid.TryParse(parts[1], out var sid))
        {
            _sessionId = sid;
            Environment.SetEnvironmentVariable(CliContext.SessionEnv, sid.ToString());
            await EnsureHubAsync(cancellationToken);
            WriteActivityLine("info", $"Session {sid}");
            return;
        }

        if (parts[0] is "create")
        {
            var name = GetFlag(parts, "--name") ?? "jz-session";
            var workspace = GetFlag(parts, "--workspace")
                ?? Environment.CurrentDirectory;
            var result = await _client.CreateSessionAsync(name, workspace, "joyzoning");
            PrintResult(result);
            if (result.IsSuccess && result.Body.HasValue &&
                result.Body.Value.TryGetProperty("id", out var idEl) &&
                Guid.TryParse(idEl.GetString(), out var created))
            {
                _sessionId = created;
                Environment.SetEnvironmentVariable(CliContext.SessionEnv, created.ToString());
                await EnsureHubAsync(cancellationToken);
            }

            return;
        }

        throw new CliUsageException("Usage: /session list | use <guid> | create --name <n> --workspace <path>");
    }

    private async Task RunTasksAsync(CancellationToken cancellationToken)
    {
        var sid = RequireSession();
        var result = await _client.ListTasksAsync(sid);
        PrintResult(result);
    }

    private void RunUse(string args)
    {
        var id = args.Trim();
        if (!Guid.TryParse(id, out var task))
            throw new CliUsageException("Usage: /use <task-guid>");
        _taskId = task;
        Environment.SetEnvironmentVariable(CliArgs.TaskIdEnv, task.ToString());
        WriteActivityLine("info", $"Active task {task}");
    }

    private async Task RunDispatchAsync(string args, CancellationToken cancellationToken)
    {
        var taskId = RequireTask();
        var approve = args.Contains("--approve-critical", StringComparison.Ordinal);
        var poll = ParseIntFlag(args, "--poll") ?? 0;
        var timeout = ParseIntFlag(args, "--timeout") ?? 600;

        var dispatchArgs = BuildArgs([
            "task", "run", taskId.ToString(),
            approve ? "--approve-critical" : "",
            poll > 0 ? "--poll" : "",
            poll > 0 ? poll.ToString() : "",
            poll > 0 ? "--timeout" : "",
            poll > 0 ? timeout.ToString() : "",
        ]);

        if (poll > 0)
            await RunWithActivityAsync(
                ct => OperatorWorkflows.RunTaskAsync(_client, dispatchArgs, taskId, ct),
                $"poll lease ({poll}s)…",
                cancellationToken);
        else
        {
            var result = await _client.DispatchTaskAsync(taskId, approve);
            PrintResult(result);
            if (result.IsSuccess)
                PrintResult(await _client.GetLeaseAsync(taskId));
        }
    }

    private async Task RunLeaseAsync(CancellationToken cancellationToken)
    {
        var taskId = RequireTask();
        PrintResult(await _client.GetLeaseAsync(taskId));
    }

    private async Task RunHeartbeatAsync(CancellationToken cancellationToken)
    {
        var taskId = RequireTask();
        var sid = RequireSession();
        PrintResult(await _client.HeartbeatLeaseAsync(taskId, sid, StatusChangeActor.Human));
    }

    private async Task RunVerifyAsync(string args, CancellationToken cancellationToken)
    {
        var taskId = RequireTask();
        var cmds = ExtractRepeatedFlag(args, "--cmd");
        if (cmds.Count == 0)
            throw new CliUsageException("Usage: /verify --cmd \"dotnet test\" [--cmd \"…\"]");

        var verifyArgs = new CliArgs
        {
            Raw = BuildVerifyRaw(taskId, cmds),
            SessionId = _sessionId,
            TaskId = taskId,
        };

        var (result, runs) = await OperatorWorkflows.VerifyTaskAsync(_client, verifyArgs, taskId, cancellationToken);
        PrintResult(result);
        foreach (var run in runs)
        {
            var mark = run.Passed ? "✓" : "✗";
            WriteActivityLine("verify", $"{mark} {run.Command} (exit {run.ExitCode})");
        }
    }

    private async Task RunCompleteAsync(string args, CancellationToken cancellationToken)
    {
        if (!args.Contains("--yes", StringComparison.Ordinal))
            throw new CliUsageException("Human merge requires: /complete --yes");

        var taskId = RequireTask();
        PrintResult(await OperatorWorkflows.CompleteTaskAsync(_client, _ctx.Args, taskId));
    }

    private async Task RunEventsAsync(string args, CancellationToken cancellationToken)
    {
        var tail = ParseIntFlag(args, "--tail") ?? 20;
        var result = await _client.ListEventsAsync(null, _taskId ?? _sessionId, null);
        if (!result.IsSuccess)
        {
            PrintResult(result);
            return;
        }

        if (result.Body is not { } body || body.ValueKind != JsonValueKind.Array)
        {
            PrintResult(result);
            return;
        }

        var items = body.EnumerateArray().TakeLast(tail);
        foreach (var ev in items)
        {
            var type = ev.TryGetProperty("type", out var t) ? t.GetString() : "?";
            WriteActivityLine("event", type ?? "?");
        }
    }

    private async Task RunApprovalsAsync(CancellationToken cancellationToken)
    {
        PrintResult(await _client.ListPendingApprovalsAsync());
    }

    private async Task RunManagerAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new CliUsageException("Usage: /manager <message>  (or type plain text without /)");

        var sid = RequireSession();
        _managerBuffer.Clear();
        WriteActivityLine("manager", "…");

        var result = await _client.SendManagerMessageAsync(sid, text);
        if (!result.IsSuccess)
        {
            PrintResult(result);
            return;
        }

        await RunWatchAsync(cancellationToken, managerOnly: true, timeoutSeconds: 300);
        lock (_streamLock)
        {
            if (_managerBuffer.Length > 0)
                Console.WriteLine();
        }
    }

    private async Task SendManagerShortcutAsync(string text, CancellationToken cancellationToken)
    {
        if (!_sessionId.HasValue)
        {
            WriteActivityLine("hint", "Set a session: /session use <id> or /session create …");
            WriteActivityLine("hint", "Agent chat: /hermes  ·  Help: /help");
            return;
        }

        await RunManagerAsync(text, cancellationToken);
    }

    private async Task RunWatchAsync(
        CancellationToken cancellationToken,
        bool managerOnly = false,
        int timeoutSeconds = 0)
    {
        await EnsureHubAsync(cancellationToken);
        _activityCts?.Cancel();
        _activityCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (timeoutSeconds > 0)
            _activityCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        WriteActivityLine("watch", managerOnly
            ? "Streaming manager reply (Ctrl+C to stop)…"
            : "Streaming live events (Ctrl+C to stop)…");

        try
        {
            await Task.Delay(Timeout.Infinite, _activityCts.Token);
        }
        catch (OperationCanceledException)
        {
            // expected
        }
    }

    private async Task RunHermesTuiAsync(string args, CancellationToken cancellationToken)
    {
        _activityCts?.Cancel();
        var (installRoot, profile) = await HermesTuiLauncher.ResolveHermesConfigAsync(_client, cancellationToken);
        var resume = args.Contains("-c", StringComparison.Ordinal) ||
                     args.Contains("--continue", StringComparison.Ordinal);
        var code = await HermesTuiLauncher.LaunchAsync(installRoot, profile, resume, cancellationToken);
        if (code != 0)
            WriteActivityLine("error", $"Hermes TUI exited with code {code}");
    }

    private async Task RunWithActivityAsync(
        Func<CancellationToken, Task<CliHttpResult>> action,
        string label,
        CancellationToken cancellationToken)
    {
        _activityCts?.Cancel();
        _activityCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        WriteActivityLine("poll", label);
        try
        {
            var result = await action(_activityCts.Token);
            PrintResult(result);
        }
        catch (OperationCanceledException)
        {
            WriteActivityLine("poll", "Interrupted.");
        }
    }

    private async Task EnsureHubAsync(CancellationToken cancellationToken)
    {
        _hub ??= new OperatorHubStream(_ctx.BaseUrl);
        if (!_hub.IsConnected)
            await _hub.ConnectAsync(cancellationToken);

        _hub.LineReceived -= OnStreamLine;
        _hub.LineReceived += OnStreamLine;

        if (_sessionId.HasValue)
            await _hub.SubscribeSessionAsync(_sessionId.Value, cancellationToken);
    }

    private void OnStreamLine(StreamLine line)
    {
        lock (_streamLock)
        {
            if (line.Kind == "manager" || line.Kind == "hermes")
            {
                if (line.IsDelta)
                {
                    Console.Write(line.Text);
                    _managerBuffer.Append(line.Text);
                }
                else
                {
                    Console.WriteLine();
                }

                return;
            }

            WriteActivityLine(line.Kind, line.Text);
        }
    }

    private void PrintResult(CliHttpResult result)
    {
        if (!result.IsSuccess)
        {
            var err = result.Message ?? result.RawText;
            WriteActivityLine("error", err);
            return;
        }

        if (result.Body is null)
        {
            if (!string.IsNullOrWhiteSpace(result.RawText))
                WriteActivityLine("ok", result.RawText);
            return;
        }

        var options = JoyZoningCliClient.JsonOptions;
        if (_prettyJson)
            options = new JsonSerializerOptions(options) { WriteIndented = true };
        WriteActivityLine("ok", JsonSerializer.Serialize(result.Body.Value, options));
    }

    private void WriteActivityLine(string kind, string text)
    {
        var prefix = kind switch
        {
            "tool" => "\x1b[36m",
            "error" => "\x1b[31m",
            "approval" => "\x1b[33m",
            "manager" => "\x1b[35m",
            "ok" => "\x1b[32m",
            _ => "\x1b[90m",
        };
        Console.WriteLine($"{prefix}┊ {kind}\x1b[0m {text}");
    }

    private void PrintBanner()
    {
        Console.WriteLine();
        Console.WriteLine("  \x1b[1mJoyZoning operator TUI\x1b[0m — human-supervised kanban runtime");
        Console.WriteLine($"  Control plane: {_ctx.BaseUrl}");
        Console.WriteLine("  /help · /hermes (full agent TUI) · plain text → Manager Chat");
        Console.WriteLine();
    }

    private Guid RequireSession()
    {
        if (_sessionId.HasValue)
            return _sessionId.Value;
        throw new CliUsageException("No session. /session list | use <id> | create --name … --workspace …");
    }

    private Guid RequireTask()
    {
        if (_taskId.HasValue)
            return _taskId.Value;
        throw new CliUsageException("No task. /use <task-id> or jz --task <id>");
    }

    private static CliArgs BuildArgs(IEnumerable<string> parts) =>
        CliArgs.Parse(parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray());

    private static string[] BuildVerifyRaw(Guid taskId, IReadOnlyList<string> cmds)
    {
        var raw = new List<string> { "task", "verify", taskId.ToString() };
        foreach (var c in cmds)
            raw.AddRange(["--cmd", c]);
        return raw.ToArray();
    }

    private static string[] SplitArgs(string args) =>
        string.IsNullOrWhiteSpace(args)
            ? []
            : args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? GetFlag(string[] parts, string flag)
    {
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i] == flag && i + 1 < parts.Length)
                return parts[i + 1];
            if (parts[i].StartsWith(flag + "=", StringComparison.Ordinal))
                return parts[i][(flag.Length + 1)..];
        }

        return null;
    }

    private static int? ParseIntFlag(string args, string flag)
    {
        var parts = SplitArgs(args);
        var v = GetFlag(parts, flag);
        return v is not null && int.TryParse(v, out var n) ? n : null;
    }

    private static List<string> ExtractRepeatedFlag(string args, string flag)
    {
        var parts = SplitArgs(args);
        var list = new List<string>();
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i] == flag && i + 1 < parts.Length)
                list.Add(parts[++i]);
            else if (parts[i].StartsWith(flag + "=", StringComparison.Ordinal))
                list.Add(parts[i][(flag.Length + 1)..]);
        }

        return list;
    }

}
