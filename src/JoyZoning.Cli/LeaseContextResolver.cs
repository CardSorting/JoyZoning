using System.Text.Json;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;

namespace JoyZoning.Cli;

public sealed record ResolvedLeaseContext(
    Guid TaskId,
    Guid SessionId,
    JsonElement Lease,
    string WorktreePath);

/// <summary>Resolve task/lease from explicit id, env, cwd worktree, or single active lease.</summary>
public static class LeaseContextResolver
{
    public static async Task<ResolvedLeaseContext> ResolveAsync(
        JoyZoningCliClient client,
        CliArgs args,
        Guid? explicitTaskId,
        CancellationToken cancellationToken = default)
    {
        if (explicitTaskId is null && args.TaskId is null)
        {
            var fileCtx = JoyZoningRuntimeContext.TryLoad();
            if (fileCtx is not null)
                return await ResolveByTaskAsync(client, fileCtx.TaskId, cancellationToken);
        }

        if (explicitTaskId is { } tid)
            return await ResolveByTaskAsync(client, tid, cancellationToken);

        if (args.TaskId is { } envTask)
            return await ResolveByTaskAsync(client, envTask, cancellationToken);

        var cwd = Path.GetFullPath(Environment.CurrentDirectory);
        var sessionId = args.SessionId;

        if (sessionId.HasValue)
        {
            var fromSession = await TryResolveFromSessionAsync(client, sessionId.Value, cwd, cancellationToken);
            if (fromSession is not null)
                return fromSession;
        }

        var fromCwd = await TryResolveFromAllSessionsAsync(client, cwd, cancellationToken);
        if (fromCwd is not null)
            return fromCwd;

        throw new CliUsageException(
            "Task id required. Pass <taskId>, --task, JOYZONING_TASK_ID, or run inside a lease worktree.");
    }

    public static async Task<ResolvedLeaseContext> ResolveByTaskAsync(
        JoyZoningCliClient client,
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        var leaseResult = await client.GetLeaseAsync(taskId);
        if (!leaseResult.IsSuccess || leaseResult.Body is null)
            throw new CliUsageException(leaseResult.Message ?? "No active lease for task.");

        var lease = leaseResult.Body.Value;
        var sessionId = ReadGuid(lease, "operatorSessionId")
            ?? ReadGuid(lease, "assignedSessionId")
            ?? throw new CliUsageException("Lease missing session id.");

        return new ResolvedLeaseContext(
            taskId,
            sessionId,
            lease,
            ReadString(lease, "worktreePath") ?? "");
    }

    private static async Task<ResolvedLeaseContext?> TryResolveFromSessionAsync(
        JoyZoningCliClient client,
        Guid sessionId,
        string cwd,
        CancellationToken cancellationToken)
    {
        var matches = await CollectActiveLeasesAsync(client, sessionId, cancellationToken);
        var cwdMatches = matches.Where(m => IsUnderWorktree(cwd, m.WorktreePath)).ToList();
        if (cwdMatches.Count == 1)
            return cwdMatches[0];

        if (matches.Count == 1)
            return matches[0];

        if (cwdMatches.Count > 1)
            throw new CliUsageException("Multiple leases match current directory; pass task id.");

        if (matches.Count > 1)
            throw new CliUsageException("Multiple active leases in session; pass task id.");

        return null;
    }

    private static async Task<ResolvedLeaseContext?> TryResolveFromAllSessionsAsync(
        JoyZoningCliClient client,
        string cwd,
        CancellationToken cancellationToken)
    {
        var sessions = await client.ListSessionsAsync();
        if (!sessions.IsSuccess || sessions.Body is null || sessions.Body.Value.ValueKind != JsonValueKind.Array)
            return null;

        var all = new List<ResolvedLeaseContext>();
        foreach (var session in sessions.Body.Value.EnumerateArray())
        {
            if (!TryGetProperty(session, "id", out var idEl) || !Guid.TryParse(idEl.GetString(), out var sessionId))
                continue;

            var matches = await CollectActiveLeasesAsync(client, sessionId, cancellationToken);
            all.AddRange(matches.Where(m => IsUnderWorktree(cwd, m.WorktreePath)));
        }

        return all.Count == 1 ? all[0] : null;
    }

    private static async Task<List<ResolvedLeaseContext>> CollectActiveLeasesAsync(
        JoyZoningCliClient client,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var list = new List<ResolvedLeaseContext>();
        var tasks = await client.ListTasksAsync(sessionId);
        if (!tasks.IsSuccess || tasks.Body is null || tasks.Body.Value.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var task in tasks.Body.Value.EnumerateArray())
        {
            if (!TryGetProperty(task, "id", out var idEl) || !Guid.TryParse(idEl.GetString(), out var taskId))
                continue;

            var status = TryGetProperty(task, "status", out var st) ? st.GetInt32() : -1;
            if (status is (int)WorkTaskStatus.Complete or (int)WorkTaskStatus.Backlog)
                continue;

            var leaseResult = await client.GetLeaseAsync(taskId);
            if (!leaseResult.IsSuccess || leaseResult.Body is null)
                continue;

            var lease = leaseResult.Body.Value;
            var leaseStatus = TryGetProperty(lease, "status", out var ls) ? ls.GetInt32() : -1;
            if (leaseStatus is (int)ExecutionLeaseStatus.Revoked or (int)ExecutionLeaseStatus.Merged)
                continue;

            list.Add(new ResolvedLeaseContext(
                taskId,
                sessionId,
                lease,
                ReadString(lease, "worktreePath") ?? ""));
        }

        return list;
    }

    private static bool IsUnderWorktree(string cwd, string worktreePath)
    {
        if (string.IsNullOrWhiteSpace(worktreePath))
            return false;

        try
        {
            var wt = Path.GetFullPath(worktreePath.TrimEnd(Path.DirectorySeparatorChar));
            return cwd.Equals(wt, StringComparison.OrdinalIgnoreCase)
                || cwd.StartsWith(wt + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static Guid? ReadGuid(JsonElement el, string name) =>
        TryGetProperty(el, name, out var p) && Guid.TryParse(p.GetString(), out var g) ? g : null;

    private static string? ReadString(JsonElement el, string name) =>
        TryGetProperty(el, name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static bool TryGetProperty(JsonElement el, string name, out JsonElement value)
    {
        if (el.TryGetProperty(name, out value))
            return true;
        var camel = char.ToLowerInvariant(name[0]) + name[1..];
        return el.TryGetProperty(camel, out value);
    }
}
