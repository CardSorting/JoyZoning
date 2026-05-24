namespace JoyZoning.Domain.Orchestration;

/// <summary>JoyZoning Sequential Delivery Protocol — canonical rules embedded in handoffs and chains.</summary>
public static class JsdpProtocol
{
    public const string Name = "JoyZoning Sequential Delivery Protocol";
    public const string ShortName = "JSDP";
    public const string ProtocolId = "JSDP";
    public const string RelativeDocPath = "docs/jsdp.md";

    public static readonly IReadOnlyList<string> RequiredOutputSections =
    [
        "Goal",
        "Scope",
        "Planned Changes",
        "Risks",
        "Deliverables",
        "Completion Criteria",
        "Follow-Up Notes",
    ];

    public static readonly IReadOnlyList<string> GlobalHandoffRules =
    [
        "Sequential execution only — you are one role in a chain; do not solve future roles.",
        "Shared canonical workspace — extend accepted work; do not fork competing architectures.",
        "Mandatory convergence gate — stop at ReadyForReview; operator accept-merge before the next role.",
        "Preserve prior accepted intent — log improvements as Follow-Up Notes, do not implement them now.",
        "Prevent concept drift — respect product lock and architecture lock from upstream roles.",
        "Local reasoning only — complete this role's scope; avoid architecture astronautics.",
        "Stable boring systems — prefer modification over reinvention; minimize surface area.",
        "Do not redesign the whole app — modify within role scope only.",
        "Do not expand scope without operator escalation — log discoveries as Follow-Up Notes.",
    ];

    public static readonly IReadOnlyList<string> ScopeGuardrailsCommon =
    [
        "Do not rewrite the universe or redesign the app every pass.",
        "Unrelated discoveries belong in Follow-Up Notes, not in this role's deliverables.",
    ];

    public static readonly IReadOnlyList<string> ScopeGuardrailsProductLock =
    [
        "Product Lock only — define purpose, users, goals, non-goals in docs/product-lock.md.",
        "Do not scaffold code or architecture in this role.",
    ];

    public static readonly IReadOnlyList<string> ScopeGuardrailsArchitectureLock =
    [
        "Architecture Lock only — stabilize structure in docs/architecture-lock.md.",
        "Minimal shell allowed if none exists; no feature screen logic.",
    ];

    public static readonly IReadOnlyList<string> ScopeGuardrailsDownstream =
    [
        "Read and respect docs/product-lock.md and docs/architecture-lock.md before changing code.",
        "Cite which lock constraints your changes honor in Completion Criteria.",
        "Prefer modification over reinvention; minimize surface area.",
    ];

    public static string ExecutorHandoffSection =>
        $"""
        ### {ShortName} ({Name}) — REQUIRED
        Agents perform choreography, not simultaneous collaboration. Goal: stable convergence.
        Protocol id: {ProtocolId}

        Global rules:
        {string.Join("\n", GlobalHandoffRules.Select(r => $"- {r}"))}

        Required role output (MUST appear in verification summary or role deliverable docs):
        {string.Join("\n", RequiredOutputSections.Select(s => $"- **{s}**"))}

        Verification without all seven sections will be **rejected** for JSDP bounded-role sessions.

        Do NOT: rewrite the universe, redesign every pass, expand scope recursively, or bypass the convergence gate.
        Full protocol: {RelativeDocPath}
        """;
}
