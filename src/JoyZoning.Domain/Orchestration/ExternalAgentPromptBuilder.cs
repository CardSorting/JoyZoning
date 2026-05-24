using JoyZoning.Domain.Entities;

namespace JoyZoning.Domain.Orchestration;

public static class ExternalAgentPromptBuilder
{
    public static string Build(
        string projectName,
        WorkTask task,
        OperatorSession session,
        string workspacePath,
        string branchName,
        string allowedAreas)
    {
        var roleName = task.Title;
        var roleGoal = ExtractSection(task.Description, "goal") ?? task.Title;
        var roleScope = task.Description;

        return $"""
            You are working inside a JoyZoning JSDP external-agent task.

            Workspace:
            {workspacePath}

            Branch:
            {branchName}

            Project:
            {projectName}

            Role:
            {roleName}

            Goal:
            {roleGoal}

            Scope:
            {roleScope}

            Allowed changes:
            {allowedAreas}

            Do not:
            - redesign accepted architecture
            - edit unrelated features
            - introduce speculative infrastructure
            - continue after the assigned role is complete

            Preserve:
            - PRODUCT_LOCK.md or docs/product-lock.md
            - ARCHITECTURE_LOCK.md or docs/architecture-lock.md
            - existing app patterns

            JSDP rules:
            - JoyZoning owns task state, branch state, verification, review, and merge
            - Do not mark the task complete — the operator reviews, verifies, and merges
            - Stop when the assigned role is done and the diff is reviewable

            Required output:
            1. Goal
            2. Scope
            3. Planned Changes
            4. Risks
            5. Deliverables
            6. Completion Criteria
            7. Follow-Up Notes

            Stop when:
            - assigned flow works
            - changes are coherent
            - operator can review the diff

            After editing, do not mark the task complete.
            The operator will review, verify, and merge.
            """;
    }

    public static string DefaultAllowedAreas(WorkTask task, OperatorSession session) =>
        JsdpSessionPolicy.RequiresEnforcement(session)
            ? "Paths and deliverables described in the role scope; lock docs only when the role explicitly requires them."
            : "Task description and session workspace; avoid unrelated systems.";

    private static string? ExtractSection(string description, string keyword)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        foreach (var line in description.Split('\n'))
        {
            if (line.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return line.Trim();
        }

        return null;
    }
}
