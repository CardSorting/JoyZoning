using JoyZoning.Domain.Agents;
using JoyZoning.Domain.Enums;
using JoyZoning.Agents.Hermes;

namespace JoyZoning.Agents;

public class AgentAdapterRegistry
{
    private readonly Dictionary<AgentKind, IAgentAdapter> _adapters;

    public AgentAdapterRegistry(IEnumerable<IAgentAdapter> adapters)
    {
        _adapters = adapters.ToDictionary(a => a.Kind);
    }

    public IAgentAdapter Get(AgentKind kind) =>
        _adapters.TryGetValue(kind, out var adapter)
            ? adapter
            : throw new ArgumentOutOfRangeException(nameof(kind), kind, "No adapter registered");
}
