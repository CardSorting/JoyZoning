using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

public sealed record RoleDeliveryChainRoleSpec(
    string Title,
    string Description,
    AgentKind AssignedAgent = AgentKind.DietCode,
    RiskLevel Risk = RiskLevel.Low);

public sealed record RoleDeliveryChainCreateResult(
    Guid ChainId,
    string WorkspaceRoot,
    string ProgramName,
    string Protocol,
    IReadOnlyList<RoleDeliveryChainMember> Members);

public sealed record RoleDeliveryChainMember(
    Guid SessionId,
    string SessionName,
    int Sequence,
    Guid TaskId,
    string TaskTitle);

/// <summary>Default delivery chains for sequential bounded-role sessions.</summary>
public static class RoleDeliveryChainTemplates
{
    /// <summary>JSDP recommended 8-role chain (default for POST /api/delivery-chains).</summary>
    public static IReadOnlyList<RoleDeliveryChainRoleSpec> DefaultEightRoles { get; } =
    [
        new(
            "Role 1 — Product Lock",
            """
            JSDP role 1. Lock app purpose, users, goals, and non-goals.
            Deliverables: docs/product-lock.md (Goal, Scope, Planned Changes, Risks, Deliverables, Completion Criteria, Follow-Up Notes).
            Exclude: implementation code, architecture scaffolding.
            """),
        new(
            "Role 2 — Architecture Lock",
            """
            JSDP role 2. Stabilize structure and canonical system boundaries.
            Deliverables: docs/architecture-lock.md, minimal shell/structure only if none exists.
            Exclude: feature screen logic, unrelated refactors.
            """),
        new(
            "Role 3 — Core Flow",
            """
            JSDP role 3. Implement or repair the main user journey.
            Deliverables: working core flow E2E within allowed paths.
            Exclude: polish, unrelated features, architecture rewrites.
            """),
        new(
            "Role 4 — UI Coherence",
            """
            JSDP role 4. Make interactions visually and behaviorally consistent.
            Deliverables: shared UI/theme updates, docs/ui-coherence.md if needed.
            Exclude: new features, persistence rewrites.
            """),
        new(
            "Role 5 — Data & Persistence",
            """
            JSDP role 5. Stabilize state, storage, and recovery behavior.
            Deliverables: persistence layer, hydration, migrations/tests as needed.
            Exclude: UI redesign, unrelated domain logic.
            """),
        new(
            "Role 6 — QA Pass",
            """
            JSDP role 6. Verify flows, identify regressions, reduce uncertainty.
            Deliverables: tests, docs/qa-pass.md, release-checklist draft.
            Exclude: feature rewrites unless fixing verified defects.
            """),
        new(
            "Role 7 — Polish & Recovery",
            """
            JSDP role 7. Fix highest-impact usability and reliability issues.
            Deliverables: targeted fixes only; document deferred items in Follow-Up Notes.
            Exclude: scope expansion, paradigm shifts.
            """),
        new(
            "Role 8 — Release Seal",
            """
            JSDP role 8. Produce final runbook, release notes, and completion verification.
            Deliverables: README updates, docs/release-seal.md, ship verification.
            Exclude: new features; integration and documentation only.
            """),
    ];

    /// <summary>Domain-specific 8-role mobile chain (TinyQuest-style).</summary>
    public static IReadOnlyList<RoleDeliveryChainRoleSpec> MobileCampfireEightRoles { get; } =
    [
        new("Role 1 — Product Architect",
            "docs/product-spec.md, docs/user-flows.md, docs/acceptance-criteria.md, docs/task-breakdown.md"),
        new("Role 2 — Mobile Architecture Lead",
            "Expo Router shell, feature structure, shared/types, docs/mobile-architecture.md — shell only"),
        new("Role 3 — Design System / UX Lead",
            "shared/ui, shared/theme, theme tokens, docs/design-system.md"),
        new("Role 4 — Quest Domain Engineer",
            "features/quests, quest models, store, Add Quest + Detail flows"),
        new("Role 5 — Brain Dump / Journal Engineer",
            "features/brain-dump, features/journal, parsers, journal persistence UX"),
        new("Role 6 — Persistence / Reliability Engineer",
            "shared/storage AsyncStorage layer, hydration, migrations, tests/shared/storage"),
        new("Role 7 — QA / Accessibility Engineer",
            "tests, a11y audit, docs/release-checklist.md"),
        new("Role 8 — Integration / Release Captain",
            "README, conflict resolution, final ship report, integration fixes"),
    ];
}
