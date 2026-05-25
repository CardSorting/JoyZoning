using JoyZoning.Jsdp.Models;

namespace JoyZoning.Jsdp.Services;

public sealed class JSDPPlanGenerator
{
    public JsdpRun Generate(
        ProjectSpecAnalysis analysis,
        string goal,
        JsdpPlanningMode mode,
        JsdpConfig? config = null,
        RepoScanSnapshot? repoScan = null)
    {
        var runId = Guid.NewGuid().ToString("N")[..12];
        var nodes = mode switch
        {
            JsdpPlanningMode.SystemsFirst => BuildSystemsFirst(analysis, goal, config, repoScan),
            JsdpPlanningMode.RiskFirst => BuildRiskFirst(analysis, goal, config, repoScan),
            _ => BuildVerticalSlices(analysis, goal, config, repoScan),
        };

        PromptDAGBuilder.WireNextLinks(nodes);

        return new JsdpRun
        {
            Id = runId,
            Goal = goal,
            PlanningMode = mode,
            CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
            Nodes = nodes.ToDictionary(n => n.Id, n => n, StringComparer.Ordinal),
        };
    }

    private static List<JsdpNode> BuildVerticalSlices(
        ProjectSpecAnalysis analysis, string goal, JsdpConfig? config, RepoScanSnapshot? repoScan)
    {
        var systems = NormalizeSystems(analysis, repoScan);
        var verify = ResolveVerification(analysis, config);
        var surface = DefaultMutationSurface(analysis, repoScan);

        return
        [
            Node("001", $"Bootstrap runtime for {ShortGoal(goal)}",
                $"Establish minimal runnable shell for {analysis.ProductGoal} using {JoinStack(analysis)}.",
                [], verify, surface, ["src/", "scripts/", "package.json", "global.json"]),
            Node("002", $"Implement core domain model for {systems[0]}",
                $"Model entities and invariants for {systems[0]} aligned with {JoinConcepts(analysis)}.",
                ["001"], verify, surface, ["src/", "tests/"]),
            Node("003", $"Wire persistence and state for {systems.ElementAtOrDefault(1) ?? systems[0]}",
                $"Add durable storage and migration-safe state transitions for {systems.ElementAtOrDefault(1) ?? "core workflow"}.",
                ["002"], verify, surface, ["src/", "migrations/", "tests/"]),
            Node("004", $"Add orchestration runtime for {systems.ElementAtOrDefault(2) ?? "execution flow"}",
                $"Connect dispatch, scheduling, and bounded mutation surfaces for {analysis.ProductGoal}.",
                ["003"], verify, surface, ["src/", "tests/"]),
            Node("005", $"Integrate operator surface for {ShortGoal(goal)}",
                $"Expose CLI/UI hooks with verification gates and resumable checkpoints.",
                ["004"], verify, surface, ["src/", "apps/", "docs/"]),
            Node("006", $"End-to-end convergence slice for {ShortGoal(goal)}",
                $"Deliver one vertical path from input to verified output with acceptance criteria met.",
                ["005"], verify, surface, ["src/", "tests/", "docs/"]),
        ];
    }

    private static List<JsdpNode> BuildSystemsFirst(
        ProjectSpecAnalysis analysis, string goal, JsdpConfig? config, RepoScanSnapshot? repoScan)
    {
        var systems = NormalizeSystems(analysis, repoScan);
        var verify = ResolveVerification(analysis, config);
        var surface = DefaultMutationSurface(analysis, repoScan);

        return
        [
            Node("001", $"Domain model for {systems[0]}",
                $"Define canonical types, boundaries, and invariants for {systems[0]}.",
                [], verify, surface, ["src/", "tests/"]),
            Node("002", $"Event and messaging contracts across {JoinSystems(systems)}",
                $"Introduce append-only operational events and correlation identifiers.",
                ["001"], verify, surface, ["src/", "tests/"]),
            Node("003", $"Persistence layer for {systems.ElementAtOrDefault(1) ?? systems[0]}",
                $"Implement repositories, migrations, and recoverable checkpoints.",
                ["002"], verify, surface, ["src/", "migrations/"]),
            Node("004", $"Runtime orchestration for {analysis.ProductGoal}",
                $"Compose services with deterministic execution order and bounded surfaces.",
                ["003"], verify, surface, ["src/", "scripts/"]),
            Node("005", $"Control-plane API integration for {ShortGoal(goal)}",
                $"Expose stable HTTP/CLI contracts with verification-aware progression.",
                ["004"], verify, surface, ["src/", "apps/"]),
            Node("006", $"Operator habitat integration",
                $"Wire JoyZoning-style review gates, ledger semantics, and resumability.",
                ["005"], verify, surface, ["docs/", "src/", ".jsdp/"]),
        ];
    }

    private static List<JsdpNode> BuildRiskFirst(
        ProjectSpecAnalysis analysis, string goal, JsdpConfig? config, RepoScanSnapshot? repoScan)
    {
        var systems = NormalizeSystems(analysis, repoScan);
        var verify = ResolveVerification(analysis, config);
        var riskVerify = analysis.VerificationStrategy.Count > 0
            ? analysis.VerificationStrategy.Take(3).ToList()
            : verify;

        return
        [
            Node("001", $"Validate runtime constraints for {ShortGoal(goal)}",
                $"Spike memory/CPU/thread limits for {JoinStack(analysis)} before feature work.",
                [], riskVerify, ["src/", "scripts/"], ["benchmark/", "tests/"]),
            Node("002", $"Verify parallel orchestration safety in {systems[0]}",
                $"Prove lease/idempotency/replay behavior under concurrent dispatch.",
                ["001"], riskVerify, ["src/", "tests/"], ["tests/"]),
            Node("003", $"Test deterministic replay for {systems.ElementAtOrDefault(1) ?? systems[0]}",
                $"Ensure operational history is append-only and recoverable after interruption.",
                ["002"], riskVerify, ["src/", ".jsdp/"], ["tests/"]),
            Node("004", $"Prove verification gate enforcement",
                $"Demonstrate failed convergence blocks progression and triggers repair lineage.",
                ["003"], verify, ["src/", "tests/"], ["tests/", ".jsdp/"]),
            Node("005", $"Implement minimal vertical slice after risks retired",
                $"Ship smallest end-to-end path once feasibility spikes pass.",
                ["004"], verify, DefaultMutationSurface(analysis, repoScan), ["src/", "tests/", "docs/"]),
        ];
    }

    private static JsdpNode Node(
        string id,
        string title,
        string intent,
        IReadOnlyList<string> deps,
        IReadOnlyList<string> verification,
        IReadOnlyList<string> mutationSurface,
        IReadOnlyList<string>? outputs = null) => new()
    {
        Id = id,
        Title = title,
        Intent = intent,
        Prompt = intent,
        Dependencies = deps.ToList(),
        AcceptanceCriteria =
        [
            $"{title} is implemented within declared mutation surface only.",
            "Verification commands pass with zero silent skips.",
            "Operational summary recorded for ledger append.",
        ],
        VerificationCommands = verification.ToList(),
        AllowedMutationSurface = mutationSurface.ToList(),
        Status = JsdpNodeStatus.Pending,
        Outputs = outputs?.ToList() ?? [],
    };

    private static List<string> NormalizeSystems(ProjectSpecAnalysis analysis, RepoScanSnapshot? repoScan)
    {
        if (analysis.CoreSystems.Count > 0)
            return analysis.CoreSystems.Take(6).ToList();

        if (repoScan?.TopLevelDirectories.Count > 0)
            return repoScan.TopLevelDirectories.Take(6).Select(d => d.TrimEnd('/')).ToList();

        if (analysis.DomainConcepts.Count > 0)
            return analysis.DomainConcepts.Take(6).ToList();

        return ["control plane", "domain layer", "persistence", "operator CLI"];
    }

    private static List<string> ResolveVerification(ProjectSpecAnalysis analysis, JsdpConfig? config)
    {
        if (config is not null &&
            config.VerificationPresets.TryGetValue(config.DefaultVerificationPreset, out var preset) &&
            preset.Count > 0)
            return preset.Take(5).ToList();

        if (analysis.VerificationStrategy.Count > 0)
            return analysis.VerificationStrategy.Take(4).ToList();

        if (analysis.TechStack.Any(s => s.Contains("dotnet", StringComparison.OrdinalIgnoreCase)))
            return ["dotnet build", "./scripts/run-tests.sh fast"];

        if (analysis.TechStack.Any(s => s.Contains("node", StringComparison.OrdinalIgnoreCase) || s.Contains("npm", StringComparison.OrdinalIgnoreCase)))
            return ["npm test", "npm run typecheck"];

        return ["echo \"define project verification in spec\""];
    }

    private static List<string> DefaultMutationSurface(ProjectSpecAnalysis analysis, RepoScanSnapshot? repoScan)
    {
        if (repoScan?.SuggestedMutationSurfaces.Count > 0)
            return repoScan.SuggestedMutationSurfaces.Take(6).ToList();

        return analysis.CoreSystems.Count > 0
            ? analysis.CoreSystems.Take(4).Select(s => s.EndsWith('/') ? s : $"{s}/").ToList()
            : ["src/", "tests/", "docs/"];
    }

    private static string ShortGoal(string goal) =>
        goal.Length <= 48 ? goal : goal[..45] + "...";

    private static string JoinStack(ProjectSpecAnalysis a) =>
        a.TechStack.Count > 0 ? string.Join(", ", a.TechStack.Take(4)) : "project stack";

    private static string JoinConcepts(ProjectSpecAnalysis a) =>
        a.DomainConcepts.Count > 0 ? string.Join(", ", a.DomainConcepts.Take(3)) : "domain vocabulary";

    private static string JoinSystems(IReadOnlyList<string> systems) =>
        string.Join(", ", systems.Take(3));
}
