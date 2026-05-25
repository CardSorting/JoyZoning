using System.Text.Json;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

public static class HandoffPacketBuilder
{
    public static HandoffPacket Build(
        WorkTask task,
        string worktreePath,
        string branchName,
        string sessionWorkspaceRoot = "",
        WorktreeSeeder.SeedResult? seedResult = null,
        OperatorSession? session = null)
    {
        var risk = LeaseRiskMapper.FromTaskRisk(task.Risk);
        var role = DeliveryRoleClassifier.Classify(task);
        var hasFoundation = WorktreeSeeder.HasProjectFoundation(sessionWorkspaceRoot)
            || (seedResult?.FilesCopied ?? 0) > 0;
        var (allowed, forbidden, extraConstraints) = ScopePathsForRole(role, task, hasFoundation);
        var isJsdp = JsdpSessionPolicy.RequiresEnforcement(session);
        var compliance = JsdpHandoffCompliance.ValidateTaskDescription(task.Description);

        var constraints = new List<string>
        {
            $"{JsdpProtocol.ShortName}: sequential choreography — one bounded role, stable convergence.",
            "Single bounded role: this session runs one agent on one role at a time.",
            "Do not mark the kanban card done.",
            "Use only the allowed paths for file changes.",
            "Stop and set status to blocked if requirements are unclear.",
        };
        constraints.AddRange(extraConstraints);

        if (isJsdp)
        {
            constraints.AddRange(JsdpHandoffCompliance.ScopeGuardrails(session!.DeliverySequence));
            constraints.Add(
                "Work in the canonical session workspace only — do not edit .joyzoning/worktrees or .joyzoning/live.");

            foreach (var warning in compliance.Warnings)
                constraints.Add($"JSDP compliance warning: {warning}");
        }

        var objective = isJsdp && !compliance.Compliant
            ? JsdpHandoffCompliance.EnsureRoleDescription(
                task.Title, task.Description, session!.DeliverySequence ?? 0)
            : task.Description;

        return new HandoffPacket
        {
            CardId = task.Id,
            Title = task.Title,
            Objective = objective,
            AcceptanceCriteria = ParseLines(objective),
            Constraints = constraints,
            AllowedPaths = allowed,
            ForbiddenPaths = forbidden,
            VerificationCommands = DefaultVerificationCommands(task, risk),
            RollbackInstructions = isJsdp
                ? $"Revert uncommitted changes in the canonical workspace and discard branch {branchName} if execution is revoked."
                : $"Discard branch {branchName} and revert uncommitted changes at {worktreePath} if execution is revoked.",
            RiskLevel = risk,
            WorktreePath = worktreePath,
            BranchName = branchName,
            SessionWorkspaceRoot = sessionWorkspaceRoot,
            DeliveryRole = role,
            DeliveryWave = 0,
            FoundationFilesSeeded = seedResult?.FilesCopied ?? 0,
            Protocol = isJsdp ? JsdpProtocol.ProtocolId : null,
            JsdpSequence = session?.DeliverySequence,
            DeliveryChainId = session?.DeliveryChainId,
            MergeGateRequired = isJsdp,
            RequiredOutputSections = isJsdp ? JsdpProtocol.RequiredOutputSections : Array.Empty<string>(),
            ComplianceWarnings = compliance.Warnings,
        };
    }

    public static string Serialize(HandoffPacket packet) =>
        JsonSerializer.Serialize(packet);

    public static HandoffPacket Deserialize(string json) =>
        JsonSerializer.Deserialize<HandoffPacket>(json)
        ?? throw new InvalidOperationException("Invalid handoff packet JSON.");

    public static string ToExecutorPrompt(HandoffPacket packet)
    {
        var criteria = packet.AcceptanceCriteria.Count > 0
            ? string.Join("\n", packet.AcceptanceCriteria.Select(c => $"- {c}"))
            : "- (derive from objective)";

        var foundation = packet.FoundationFilesSeeded > 0
            ? $"Foundation seeded: {packet.FoundationFilesSeeded} file(s) copied from session workspace."
            : WorktreeSeeder.HasProjectFoundation(packet.SessionWorkspaceRoot)
                ? "Foundation exists in session workspace — extend it; do not re-scaffold."
                : "No foundation yet — you may scaffold only within your allowed paths.";

        return $"""
            ## JoyZoning execution handoff (bounded session)
            Card: {packet.CardId}
            Title: {packet.Title}
            Risk: {packet.RiskLevel}
            Role scope: {DeliveryRoleClassifier.RoleLabel(packet.DeliveryRole)}
            Worktree: {packet.WorktreePath}
            Branch: {packet.BranchName}
            Session workspace: {packet.SessionWorkspaceRoot}

            ### Session model
            One agent, one active role per session. Complete this role before another dispatches.
            Protocol: {packet.Protocol ?? "default"}
            JSDP sequence: {packet.JsdpSequence?.ToString() ?? "n/a"}
            Merge gate required: {packet.MergeGateRequired}

            {JsdpProtocol.ExecutorHandoffSection}

            {(packet.MergeGateRequired ? JsdpProtocol.RollingHorizonHermesSection : "")}

            {(packet.ComplianceWarnings.Count > 0
                ? "### JSDP compliance warnings\n" + string.Join("\n", packet.ComplianceWarnings.Select(w => $"- {w}"))
                : "")}

            ### Objective
            {packet.Objective}

            ### Foundation
            {foundation}

            ### Acceptance criteria
            {criteria}

            ### Constraints
            {string.Join("\n", packet.Constraints.Select(c => $"- {c}"))}

            ### Allowed paths
            {string.Join(", ", packet.AllowedPaths)}

            ### Forbidden paths
            {string.Join(", ", packet.ForbiddenPaths)}

            ### Verification commands
            {string.Join("\n", packet.VerificationCommands.Select(c => $"- `{c}`"))}

            ### Rollback
            {packet.RollbackInstructions}

            You must not mark this kanban card done. When finished, move the lease to verifying, then ready_for_review after checks pass.
            """;
    }

    private static (IReadOnlyList<string> Allowed, IReadOnlyList<string> Forbidden, IReadOnlyList<string> Constraints)
        ScopePathsForRole(DeliveryRoleKind role, WorkTask task, bool hasFoundation)
    {
        var forbidden = CommonForbiddenPaths();
        var constraints = new List<string>();

        if (hasFoundation)
            constraints.Add("Do not re-create package.json, app shell, or root Expo configs unless your role explicitly owns them.");

        return role switch
        {
            DeliveryRoleKind.ProductArchitect => (
                ["docs/", "docs/product-lock.md"],
                forbidden.Concat(["app/", "features/", "shared/", "package.json", "app.json"]).ToList(),
                constraints.Concat(["Documentation only — do not scaffold the mobile app."]).ToList()),

            DeliveryRoleKind.MobileArchitect => (
                [
                    "app/", "app.json", "babel.config.js", "metro.config.js", "tsconfig.json",
                    "package.json", "package-lock.json", "expo-env.d.ts", ".npmrc",
                    "docs/mobile-architecture.md", "docs/architecture-lock.md", "features/*/config.ts", "shared/types/",
                ],
                forbidden,
                constraints.Concat([
                    "Build navigation shell and shared types only.",
                    "Do not implement feature screen logic — leave placeholders for downstream roles.",
                ]).ToList()),

            DeliveryRoleKind.DesignSystem => (
                ["shared/ui/", "shared/theme/", "docs/design-system.md", "assets/"],
                forbidden.Concat(hasFoundation ? ["package.json", "app/_layout.tsx"] : []).ToList(),
                constraints.Concat(["Extend the design system on top of the existing foundation."]).ToList()),

            DeliveryRoleKind.QuestDomain => (
                ["features/quests/", "app/(tabs)/quests.tsx", "app/quests/", "shared/types/quest.ts"],
                forbidden.Concat(["package.json", "app/_layout.tsx", "shared/storage/"]).ToList(),
                constraints.Concat(["Implement quest domain only — do not touch other features."]).ToList()),

            DeliveryRoleKind.BrainDumpJournal => (
                [
                    "features/brain-dump/", "features/journal/",
                    "app/(tabs)/brain-dump.tsx", "app/(tabs)/journal.tsx",
                    "app/brain-dump/", "app/journal/",
                    "shared/types/brain-dump.ts", "shared/types/journal.ts",
                ],
                forbidden.Concat(["package.json", "app/_layout.tsx", "shared/storage/"]).ToList(),
                constraints.Concat(["Implement brain dump and journal flows only."]).ToList()),

            DeliveryRoleKind.Persistence => (
                ["shared/storage/", "tests/shared/storage/", ".npmrc"],
                forbidden.Concat(["app/", "features/", "package.json"]).ToList(),
                constraints.Concat(["Persistence layer only — wire storage; do not scaffold UI."]).ToList()),

            DeliveryRoleKind.QaAccessibility => (
                ["tests/", "docs/release-checklist.md", "docs/a11y-audit.md"],
                forbidden,
                constraints.Concat(["Add tests and QA docs; avoid feature rewrites unless fixing defects."]).ToList()),

            DeliveryRoleKind.IntegrationCaptain => (
                ["README.md", "docs/", "package.json", "app/", "features/", "shared/"],
                forbidden,
                constraints.Concat(["Integrate, resolve conflicts, and prepare release — minimal necessary changes only."]).ToList()),

            _ when IsMobileStackTask(task) => (
                MobileAllowedPaths(),
                forbidden,
                constraints),

            _ => (
                DefaultDotnetPaths(),
                forbidden,
                constraints),
        };
    }

    private static List<string> CommonForbiddenPaths() =>
        [".git/", "node_modules/", ".env", "secrets/", ".joyzoning/"];

    public static bool IsMobileStackTask(WorkTask task)
    {
        var text = $"{task.Title} {task.Description}".ToLowerInvariant();
        return text.Contains("expo", StringComparison.Ordinal)
            || text.Contains("react native", StringComparison.Ordinal)
            || text.Contains("react-native", StringComparison.Ordinal)
            || text.Contains("mobile app", StringComparison.Ordinal)
            || text.Contains("expo router", StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> DefaultDotnetPaths() =>
        ["src/", "tests/", "docs/", "scripts/"];

    private static IReadOnlyList<string> MobileAllowedPaths() =>
    [
        "app/",
        "features/",
        "shared/",
        "assets/",
        "src/",
        "tests/",
        "docs/",
        "scripts/",
        "package.json",
        "package-lock.json",
        "app.json",
        "app.config.ts",
        "app.config.js",
        "tsconfig.json",
        "babel.config.js",
        "metro.config.js",
        "expo-env.d.ts",
        ".npmrc",
    ];

    private static IReadOnlyList<string> DefaultVerificationCommands(WorkTask task, LeaseRiskLevel risk)
    {
        if (IsMobileStackTask(task))
        {
            return risk == LeaseRiskLevel.Critical
                ? ["npm install", "npx tsc --noEmit", "npm run lint"]
                : ["npm install", "npx tsc --noEmit"];
        }

        return risk == LeaseRiskLevel.Critical
            ? ["dotnet build", "dotnet test --no-build"]
            : ["dotnet build"];
    }

    private static IReadOnlyList<string> ParseLines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.Length > 0)
            .Take(12)
            .ToList();
}
