using System.Text.Json;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

/// <summary>Local agent/harness context written under the lease worktree.</summary>
public sealed class JoyZoningRuntimeContext
{
    public const string RelativePath = ".joyzoning/context.json";
    public const string SchemaVersion = "1";

    public string SchemaVersionValue { get; init; } = SchemaVersion;
    public Guid TaskId { get; init; }
    public Guid LeaseId { get; init; }
    public Guid SessionId { get; init; }
    public string WorktreePath { get; init; } = "";
    public string ControlPlaneUrl { get; init; } = "http://127.0.0.1:9470";
    public string LeaseStatus { get; init; } = "";
    public IReadOnlyList<string> AllowedCommands { get; init; } = DefaultAllowedCommands;
    public IReadOnlyList<string> VerificationCommands { get; init; } = [];
    public IReadOnlyList<string> AuthorityRules { get; init; } = DefaultAuthorityRules;
    public IReadOnlyList<string> ForbiddenActions { get; init; } = DefaultForbiddenActions;

    public LastVerificationState? LastVerification { get; init; }

    public static readonly IReadOnlyList<string> DefaultAllowedCommands =
    [
        "jz agent heartbeat",
        "jz agent verify --cmd \"...\"",
        "jz agent blocked --reason \"...\"",
        "jz agent done",
    ];

    public static readonly IReadOnlyList<string> DefaultAuthorityRules =
    [
        "Agents cannot merge, revoke, or mark tasks Complete.",
        "agent done means ready_for_human_review, not Complete.",
        "Critical dispatch requires human --approve-critical.",
        "Work must stay inside the assigned worktree when context is present.",
    ];

    public static readonly IReadOnlyList<string> DefaultForbiddenActions =
    [
        "task complete",
        "task merge",
        "task revoke",
        "lease merge",
        "lease revoke",
        "UpdateTaskStatus to Complete",
    ];

    public static JoyZoningRuntimeContext FromLease(
        ExecutionLease lease,
        string controlPlaneUrl,
        IEnumerable<string>? verificationCommands = null,
        LastVerificationState? lastVerification = null) =>
        new()
        {
            TaskId = lease.WorkTaskId,
            LeaseId = lease.Id,
            SessionId = lease.AssignedSessionId,
            WorktreePath = lease.WorktreePath,
            ControlPlaneUrl = controlPlaneUrl.TrimEnd('/'),
            LeaseStatus = lease.Status.ToString(),
            VerificationCommands = verificationCommands?.ToList() ?? [],
            LastVerification = lastVerification,
        };

    public string AbsolutePath => Path.Combine(WorktreePath, RelativePath);

    public static void Write(ExecutionLease lease, string controlPlaneUrl,
        IEnumerable<string>? verificationCommands = null,
        LastVerificationState? lastVerification = null)
    {
        var ctx = FromLease(lease, controlPlaneUrl, verificationCommands, lastVerification);
        var dir = Path.Combine(lease.WorktreePath, ".joyzoning");
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(ctx, JsonOptions);
        File.WriteAllText(Path.Combine(dir, "context.json"), json);
    }

    public static JoyZoningRuntimeContext? TryLoad(string? startDirectory = null)
    {
        var dir = startDirectory ?? Environment.CurrentDirectory;
        for (var current = new DirectoryInfo(dir); current is not null; current = current.Parent)
        {
            var path = Path.Combine(current.FullName, RelativePath);
            if (!File.Exists(path))
                continue;

            try
            {
                return JsonSerializer.Deserialize<JoyZoningRuntimeContext>(
                    File.ReadAllText(path), JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    public static bool IsCurrentDirectoryInsideWorktree(string? cwd, string worktreePath)
    {
        if (string.IsNullOrWhiteSpace(worktreePath))
            return false;

        try
        {
            cwd ??= Environment.CurrentDirectory;
            var fullCwd = NormalizeComparablePath(Path.GetFullPath(cwd));
            var fullWt = NormalizeComparablePath(Path.GetFullPath(
                worktreePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
            return fullCwd.Equals(fullWt, StringComparison.OrdinalIgnoreCase)
                || fullCwd.StartsWith(fullWt + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>macOS often resolves /var and /tmp through /private; align before comparing.</summary>
    private static string NormalizeComparablePath(string fullPath)
    {
        if (!OperatingSystem.IsMacOS())
            return fullPath;

        if (fullPath.StartsWith("/private/var/", StringComparison.Ordinal))
            return "/var/" + fullPath["/private/var/".Length..];
        if (fullPath.StartsWith("/private/tmp/", StringComparison.Ordinal))
            return "/tmp/" + fullPath["/private/tmp/".Length..];
        return fullPath;
    }

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };
}

public sealed class LastVerificationState
{
    public DateTimeOffset At { get; init; }
    public bool AllPassed { get; init; }
    public IReadOnlyList<string> Commands { get; init; } = [];
}
