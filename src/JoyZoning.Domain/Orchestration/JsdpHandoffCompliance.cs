using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

/// <summary>Validates and augments JSDP handoffs, role descriptions, and completion reports.</summary>
public static class JsdpHandoffCompliance
{
    public const string ComplianceErrorCode = "jsdp_compliance";

    public static readonly IReadOnlyList<string> LockArtifactPaths =
    [
        "docs/product-lock.md",
        "docs/architecture-lock.md",
    ];

    public sealed record ComplianceResult(
        bool Compliant,
        IReadOnlyList<string> MissingSections,
        IReadOnlyList<string> Warnings);

    public static ComplianceResult ValidateTaskDescription(string? text)
    {
        var missing = FindMissingSections(text ?? string.Empty);
        var warnings = missing.Count > 0
            ? missing.Select(s => $"JSDP handoff missing required section: {s}").ToList()
            : new List<string>();
        return new ComplianceResult(missing.Count == 0, missing, warnings);
    }

    public static string EnsureRoleDescription(string title, string description, int sequence)
    {
        var compliance = ValidateTaskDescription(description);
        if (compliance.Compliant)
            return description;

        return $"""
            {description.Trim()}

            ## JSDP required role output (include all sections in deliverables)
            {RequiredOutputTemplate()}

            Role sequence: {sequence} — {title}
            """;
    }

    public static string RequiredOutputTemplate() =>
        string.Join("\n", RequiredOutputSections.Select(s => $"### {s}\n(fill in for this role)"));

    private static IReadOnlyList<string> RequiredOutputSections => JsdpProtocol.RequiredOutputSections;

    public static string? ValidateVerificationReport(WorkTask task, OperatorSession session, VerificationReport report)
    {
        if (!JsdpSessionPolicy.RequiresEnforcement(session))
            return null;

        var text = BuildVerificationText(report);
        var missing = FindMissingSections(text);
        if (missing.Count == 0)
            return null;

        return $"{ComplianceErrorCode}: verification summary missing JSDP sections: {string.Join(", ", missing)}. "
            + "Include Goal, Scope, Planned Changes, Risks, Deliverables, Completion Criteria, and Follow-Up Notes "
            + "in command summaries or role deliverable docs.";
    }

    public static string? ValidateLockArtifactsForDispatch(OperatorSession session, string workspaceRoot)
    {
        if (!JsdpSessionPolicy.RequiresEnforcement(session) || session.DeliverySequence is null or < 3)
            return null;

        if (!Directory.Exists(workspaceRoot))
        {
            return $"JSDP: workspace root not found ({workspaceRoot}). "
                + "Product Lock and Architecture Lock must exist before downstream roles dispatch.";
        }

        foreach (var relative in LockArtifactPaths)
        {
            var full = Path.Combine(workspaceRoot, relative);
            if (!File.Exists(full))
            {
                return $"JSDP: required lock artifact missing ({relative}). "
                    + "Complete Product Lock (role 1) and Architecture Lock (role 2) first.";
            }
        }

        return null;
    }

    public static string? ValidateDispatchHandoffCompliance(WorkTask task, OperatorSession session)
    {
        if (!JsdpSessionPolicy.RequiresEnforcement(session))
            return null;

        var compliance = ValidateTaskDescription(task.Description);
        if (compliance.Compliant)
            return null;

        return $"{ComplianceErrorCode}: task description missing JSDP sections ({string.Join(", ", compliance.MissingSections)}). "
            + "All seven sections are required before dispatch.";
    }

    public static IReadOnlyList<string> ScopeGuardrails(int? sequence) =>
        sequence switch
        {
            null or <= 0 => JsdpProtocol.ScopeGuardrailsCommon,
            1 => JsdpProtocol.ScopeGuardrailsProductLock,
            2 => JsdpProtocol.ScopeGuardrailsArchitectureLock,
            _ => JsdpProtocol.ScopeGuardrailsDownstream,
        };

    internal static IReadOnlyList<string> FindMissingSections(string text)
    {
        var normalized = text.ToLowerInvariant();
        var missing = new List<string>();
        foreach (var section in JsdpProtocol.RequiredOutputSections)
        {
            if (!normalized.Contains(section.ToLowerInvariant(), StringComparison.Ordinal))
                missing.Add(section);
        }

        return missing;
    }

    private static string BuildVerificationText(VerificationReport report)
    {
        var parts = report.CommandsRun.Select(c => c.Summary)
            .Concat(report.Risks)
            .Concat(report.UnresolvedQuestions);
        return string.Join("\n", parts);
    }
}
