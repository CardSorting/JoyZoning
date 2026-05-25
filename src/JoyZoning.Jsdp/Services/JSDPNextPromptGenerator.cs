using JoyZoning.Jsdp.Models;

namespace JoyZoning.Jsdp.Services;

public sealed class JSDPNextPromptGenerator
{
    public string Generate(JsdpNode node, JsdpRun run)
    {
        var depLines = node.Dependencies.Count == 0
            ? "- None (root node)"
            : string.Join("\n", node.Dependencies.Select(d =>
            {
                var title = run.Nodes.TryGetValue(d, out var dep) ? dep.Title : d;
                return $"- `{d}` — {title}";
            }));

        var acceptance = string.Join("\n", node.AcceptanceCriteria.Select(c => $"- {c}"));
        var verification = string.Join("\n", node.VerificationCommands.Select(c => $"- `{c}`"));
        var surface = string.Join("\n", node.AllowedMutationSurface.Select(s => $"- `{s}`"));

        return $"""
            # JSDP Node: {node.Id} — {node.Title}

            ## Goal

            {node.Intent}

            ## Scope

            Complete only the work described for node `{node.Id}`. Do not expand into downstream nodes.

            ## Dependencies

            {depLines}

            ## Acceptance Criteria

            {acceptance}

            ## Required Verification

            {verification}

            ## Allowed Mutation Surface

            {surface}

            ## Stop Condition

            Stop after this node.
            Do not proceed to future nodes.

            ## Required Response Format

            - Summary
            - Files changed
            - Verification commands run
            - Result
            - Recommended next action
            """;
    }

    public string WritePromptFile(string workspaceRoot, JsdpNode node, JsdpRun run)
    {
        var content = Generate(node, run);
        var path = JsdpPaths.PromptFile(workspaceRoot, node.Id);
        Directory.CreateDirectory(JsdpPaths.Prompts(workspaceRoot));
        File.WriteAllText(path, content);
        node.Prompt = content;
        return path;
    }
}
