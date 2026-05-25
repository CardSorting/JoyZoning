using JoyZoning.Jsdp.Models;

namespace JoyZoning.Jsdp.Services;

public sealed class JSDPContinuationEngine
{
    private readonly JSDPRepairNodeFactory _repairs = new();

    public JsdpContinuationResult Continue(JsdpRun run, string? currentNodeId = null)
    {
        var nodeId = currentNodeId ?? run.CurrentNodeId;
        if (string.IsNullOrEmpty(nodeId) || !run.Nodes.TryGetValue(nodeId, out var current))
            throw new JsdpException("No current node. Run joyzoning jsdp next first.");

        if (current.Status == JsdpNodeStatus.Verified)
        {
            var nextId = PromptDAGBuilder.FindNextReadyNode(run);
            run.CurrentNodeId = nextId;
            return new JsdpContinuationResult
            {
                Action = JsdpContinuationAction.Advanced,
                NodeId = nextId,
                Message = nextId is null
                    ? "All nodes verified. DAG converged."
                    : $"Advanced to next ready node {nextId}.",
            };
        }

        if (current.Status == JsdpNodeStatus.Failed)
        {
            var repair = _repairs.CreateRepairNode(current, run);
            run.Nodes[repair.Id] = repair;
            current.Status = JsdpNodeStatus.Blocked;
            run.CurrentNodeId = repair.Id;
            PromptDAGBuilder.WireNextLinks(run.Nodes.Values.ToList());

            return new JsdpContinuationResult
            {
                Action = JsdpContinuationAction.RepairCreated,
                NodeId = repair.Id,
                Message = $"Created repair node {repair.Id} for failed node {current.Id}.",
            };
        }

        throw new JsdpException(
            $"Cannot continue: node {current.Id} is {current.Status}. Run verify or complete work first.");
    }
}

public enum JsdpContinuationAction
{
    Advanced,
    RepairCreated,
}

public sealed class JsdpContinuationResult
{
    public JsdpContinuationAction Action { get; set; }
    public string? NodeId { get; set; }
    public string Message { get; set; } = "";
}
