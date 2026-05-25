using JoyZoning.Jsdp.Models;

namespace JoyZoning.Jsdp.Services;

public static class PromptDAGBuilder
{
    public static void WireNextLinks(IReadOnlyList<JsdpNode> nodes, IReadOnlySet<string>? externalDependencyIds = null)
    {
        var byId = nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            var dependents = nodes
                .Where(n => n.Dependencies.Contains(node.Id, StringComparer.Ordinal))
                .Select(n => n.Id)
                .ToList();
            node.Next = dependents.Count > 0 ? dependents : null;
        }

        ValidateAcyclic(nodes);
        ValidateDependenciesExist(nodes, byId, externalDependencyIds);
    }

    public static string? FindNextReadyNode(JsdpRun run)
    {
        foreach (var node in TopologicalOrder(run.Nodes.Values))
        {
            if (node.Status is JsdpNodeStatus.Verified or JsdpNodeStatus.Running)
                continue;
            if (node.Status is JsdpNodeStatus.Blocked or JsdpNodeStatus.Failed)
                continue;

            var depsReady = node.Dependencies.All(dep =>
                run.Nodes.TryGetValue(dep, out var depNode) &&
                depNode.Status == JsdpNodeStatus.Verified);

            if (depsReady)
                return node.Id;
        }

        return null;
    }

    public static IReadOnlyList<JsdpNode> TopologicalOrder(IEnumerable<JsdpNode> nodes)
    {
        var list = nodes.ToList();
        var byId = list.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var temp = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<JsdpNode>();

        void Visit(string id)
        {
            if (visited.Contains(id))
                return;
            if (!temp.Add(id))
                throw new JsdpException($"Cycle detected at node {id}");

            if (byId.TryGetValue(id, out var node))
            {
                foreach (var dep in node.Dependencies)
                    Visit(dep);
            }

            temp.Remove(id);
            visited.Add(id);
            if (byId.TryGetValue(id, out var n))
                result.Add(n);
        }

        foreach (var node in list.OrderBy(n => n.Id, StringComparer.Ordinal))
            Visit(node.Id);

        return result;
    }

    public static JsdpTreeDocument BuildTreeDocument(JsdpRun run)
    {
        var nodes = TopologicalOrder(run.Nodes.Values)
            .Select(n => new JsdpTreeNodeSummary
            {
                Id = n.Id,
                Title = n.Title,
                Status = n.Status,
                Dependencies = n.Dependencies,
                RepairOf = n.RepairOf,
            })
            .ToList();

        return new JsdpTreeDocument
        {
            RunId = run.Id,
            Goal = run.Goal,
            PlanningMode = run.PlanningMode,
            Nodes = nodes,
            Convergence = ComputeConvergence(run),
        };
    }

    public static JsdpConvergenceSummary ComputeConvergence(JsdpRun run)
    {
        var nodes = run.Nodes.Values.ToList();
        var verified = nodes.Count(n => n.Status == JsdpNodeStatus.Verified);
        var failed = nodes.Count(n => n.Status == JsdpNodeStatus.Failed);
        var blocked = nodes.Count(n => n.Status == JsdpNodeStatus.Blocked);
        var pending = nodes.Count(n => n.Status == JsdpNodeStatus.Pending);
        var running = nodes.Count(n => n.Status == JsdpNodeStatus.Running);

        var health = failed > 0 ? "degraded"
            : blocked > 0 ? "blocked"
            : verified == nodes.Count && nodes.Count > 0 ? "converged"
            : verified > 0 ? "progressing"
            : "initializing";

        return new JsdpConvergenceSummary
        {
            Total = nodes.Count,
            Verified = verified,
            Failed = failed,
            Blocked = blocked,
            Pending = pending,
            Running = running,
            Health = health,
        };
    }

    private static void ValidateAcyclic(IReadOnlyList<JsdpNode> nodes)
    {
        try
        {
            _ = TopologicalOrder(nodes);
        }
        catch (JsdpException)
        {
            throw;
        }
    }

    private static void ValidateDependenciesExist(
        IReadOnlyList<JsdpNode> nodes,
        Dictionary<string, JsdpNode> byId,
        IReadOnlySet<string>? externalDependencyIds = null)
    {
        foreach (var node in nodes)
        {
            foreach (var dep in node.Dependencies)
            {
                if (!byId.ContainsKey(dep) && externalDependencyIds?.Contains(dep) != true)
                    throw new JsdpException($"Node {node.Id} references missing dependency {dep}");
            }
        }
    }
}
