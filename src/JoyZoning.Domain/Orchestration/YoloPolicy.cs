using System.Text.Json;
using System.Text.Json.Serialization;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

/// <summary>
/// Pre-approved envelope for autonomous jz operation. Agents may drive execution;
/// humans retain merge and Complete authority.
/// </summary>
public sealed class YoloPolicy
{
    public bool Enabled { get; set; }

    public Guid SessionId { get; set; }

    /// <summary>Maximum task risk YOLO may pick up: Low or Medium only.</summary>
    public string MaxRiskLevel { get; set; } = "medium";

    public List<string> AllowedTaskTags { get; set; } = [];

    public List<string> ForbiddenTaskTags { get; set; } = [];

    public int MaxTasksPerRun { get; set; } = 3;

    public int MaxRuntimeMinutes { get; set; } = 45;

    public int MaxVerificationRetries { get; set; } = 2;

    /// <summary>Seconds to wait for lease to reach Running after dispatch (executor settle).</summary>
    public int DispatchSettleTimeoutSeconds { get; set; } = 600;

    /// <summary>Poll interval while waiting for executor after dispatch.</summary>
    public int DispatchPollIntervalSeconds { get; set; } = 5;

    /// <summary>Heartbeat interval while waiting for executor (keeps lease fresh).</summary>
    public int HeartbeatIntervalSeconds { get; set; } = 45;

    public bool AllowRecover { get; set; }

    /// <summary>When false (default), YOLO never calls revoke — human operators only.</summary>
    public bool AllowRevoke { get; set; }

    public bool RequireCleanWorktreeBeforeStart { get; set; } = true;

    /// <summary>Union handoff verification commands with policy required commands.</summary>
    public bool MergeHandoffVerificationCommands { get; set; } = true;

    public List<string> RequiredVerificationCommands { get; set; } = ["dotnet build", "dotnet test"];

    public List<string> ForbiddenCommands { get; set; } =
    [
        "git push",
        "git reset --hard",
        "rm -rf",
        "sudo ",
    ];

    /// <summary>Always true — YOLO cannot disable human merge.</summary>
    public bool RequireHumanMerge { get; set; } = true;

    public static YoloPolicy LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"YOLO policy file not found: {path}");

        var json = File.ReadAllText(path);
        var policy = JsonSerializer.Deserialize<YoloPolicy>(json, JsonOptions)
            ?? throw new InvalidOperationException("YOLO policy file is empty or invalid.");

        policy.Validate();
        return policy;
    }

    public void Validate()
    {
        if (SessionId == Guid.Empty)
            throw new InvalidOperationException("YOLO policy requires sessionId.");

        if (!Enabled)
            throw new InvalidOperationException("YOLO policy must have enabled: true.");

        var max = MaxRiskLevel.Trim().ToLowerInvariant();
        if (max is not "low" and not "medium")
            throw new InvalidOperationException("maxRiskLevel must be 'low' or 'medium'.");

        if (MaxTasksPerRun < 1)
            throw new InvalidOperationException("maxTasksPerRun must be at least 1.");

        if (MaxRuntimeMinutes < 1)
            throw new InvalidOperationException("maxRuntimeMinutes must be at least 1.");

        if (MaxVerificationRetries < 0)
            throw new InvalidOperationException("maxVerificationRetries cannot be negative.");

        if (DispatchSettleTimeoutSeconds < 1)
            throw new InvalidOperationException("dispatchSettleTimeoutSeconds must be at least 1.");

        if (DispatchPollIntervalSeconds < 1)
            throw new InvalidOperationException("dispatchPollIntervalSeconds must be at least 1.");

        if (HeartbeatIntervalSeconds < 1)
            throw new InvalidOperationException("heartbeatIntervalSeconds must be at least 1.");

        if (AllowRevoke)
            throw new InvalidOperationException(
                "allowRevoke must remain false; YOLO does not perform revoke (human operators only).");

        if (!RequireHumanMerge)
            throw new InvalidOperationException("requireHumanMerge must remain true.");

        if (RequiredVerificationCommands.Count == 0)
            throw new InvalidOperationException("requiredVerificationCommands must not be empty.");

        ValidateCommands(RequiredVerificationCommands, "requiredVerificationCommands");
        ValidateCommands(ForbiddenCommands, "forbiddenCommands");
    }

    public RiskLevel MaxRiskLevelEnum() => MaxRiskLevel.Trim().ToLowerInvariant() switch
    {
        "low" => RiskLevel.Low,
        "medium" => RiskLevel.Medium,
        _ => throw new InvalidOperationException($"Unsupported maxRiskLevel: {MaxRiskLevel}"),
    };

    public void AssertCommandAllowed(string command)
    {
        var normalized = command.Trim();
        foreach (var forbidden in ForbiddenCommands)
        {
            if (string.IsNullOrWhiteSpace(forbidden))
                continue;
            if (normalized.Contains(forbidden.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new YoloPolicyViolationException(
                    $"Verification command violates forbiddenCommands: {command}");
        }
    }

    private static void ValidateCommands(IEnumerable<string> commands, string fieldName)
    {
        foreach (var cmd in commands)
        {
            if (string.IsNullOrWhiteSpace(cmd))
                throw new InvalidOperationException($"{fieldName} cannot contain blank entries.");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}

public sealed class YoloPolicyViolationException(string message) : Exception(message);
