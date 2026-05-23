"""
BroccoliDB Graph Tools — Knowledge graph CRUD, search, and memory.

These tools provide the agent with epistemic graph capabilities:
adding knowledge nodes, querying the graph, fetching task context,
contributing to shared memory, and performing Skeptical Audits via
structural/epistemic sovereignty verification.

Design principles (mirroring industry standards):
  - Skeptical Audits: Epistemic validation incorporating git signals, evidence
    discounting, and adaptive calibration to detect stale, low-confidence, or
    contradictory knowledge in the graph.
"""
import json
from tools.registry import registry
from tools.broccolidb_tools.runner import (
    check_requirements,
    run_agent_context_script,
)


# ─── Handlers ───

def broccolidb_add_knowledge(kb_id: str, type: str, content: str, tags: str = None) -> str:
    """Add a new cognitive node to the BroccoliDB Knowledge Graph."""
    body = f"""\
  const tagsStr = {repr(tags or "")};
  const tagsArray = tagsStr ? tagsStr.split(',').map(t => t.trim()).filter(Boolean) : [];
  const newId = await context.addKnowledge(
    {repr(kb_id)},
    {repr(type)},
    {repr(content)},
    {{ tags: tagsArray }}
  );
  console.log(JSON.stringify({{ success: true, kbId: newId }}));
"""
    return run_agent_context_script(body)


def broccolidb_query_graph(query: str, tags: str = None, limit: int = 10) -> str:
    """Search the BroccoliDB Knowledge Graph with confidence metrics."""
    body = f"""\
  const tagsStr = {repr(tags or "")};
  const tagsArray = tagsStr ? tagsStr.split(',').map(t => t.trim()).filter(Boolean) : undefined;
  const results = await context.searchKnowledge({repr(query)}, tagsArray, {limit});
  console.log(JSON.stringify({{
    success: true,
    resultCount: results.length,
    results: results.map(r => ({{
      id: r.itemId,
      type: r.type,
      content: r.content.substring(0, 500),
      confidence: r.confidence,
      tags: r.tags,
      edgeCount: (r.edges || []).length,
    }})),
  }}));
"""
    return run_agent_context_script(body)


def broccolidb_get_task_context(task_id: str) -> str:
    """Retrieve contextual intelligence for a task."""
    body = f"""\
  const taskContext = await context.getTaskContext({repr(task_id)});
  console.log(JSON.stringify({{
    success: true,
    taskId: {repr(task_id)},
    context: taskContext,
  }}));
"""
    return run_agent_context_script(body)


def broccolidb_append_shared_memory(memory: str) -> str:
    """Contribute a global guideline to the swarm-wide Shared Rulebook."""
    body = f"""\
  await context.appendSharedMemory({repr(memory)});
  console.log(JSON.stringify({{ success: true, message: 'Memory appended to shared rulebook.' }}));
"""
    return run_agent_context_script(body)


def broccolidb_verify_sovereignty(kb_id: str) -> str:
    """Verify structural & epistemic sovereignty of a knowledge node.

    This performs a 'Skeptical Audit' on the knowledge node, incorporating
    git distance signals, evidence discounting, and adaptive threshold calibration.
    """
    body = f"""\
  const nodeId = {repr(kb_id)};
  const result = await context.reasoningService.verifySovereignty(nodeId);
  const caveat = await context.reasoningService.getSovereignCaveat(nodeId);

  console.log(JSON.stringify({{
    success: true,
    kbId: nodeId,
    isValid: result.isValid,
    metrics: result.metrics,
    caveat: caveat || null,
    verdict: result.isValid ? 'SOVEREIGN' : 'UNRELIABLE',
  }}));
"""
    return run_agent_context_script(body)


# ─── Registrations ───

registry.register(
    name="broccolidb_add_knowledge",
    toolset="broccolidb",
    schema={
        "name": "broccolidb_add_knowledge",
        "description": (
            "Add a knowledge node (fact, rule, hypothesis, or conclusion) to the "
            "BroccoliDB Knowledge Graph. Use this to persist architectural decisions, "
            "verified facts, or working hypotheses. Nodes can be linked and queried later."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "kb_id": {
                    "type": "string",
                    "description": "Unique ID for the node, or 'auto' to auto-generate",
                },
                "type": {
                    "type": "string",
                    "enum": ["fact", "vector", "rule", "hypothesis", "conclusion"],
                    "description": "The category of knowledge",
                },
                "content": {
                    "type": "string",
                    "description": "The text payload of the knowledge node",
                },
                "tags": {
                    "type": "string",
                    "description": "Optional comma-separated tags (e.g., 'rules,architecture')",
                },
            },
            "required": ["kb_id", "type", "content"],
        },
    },
    handler=lambda args, **kw: broccolidb_add_knowledge(
        kb_id=args.get("kb_id"),
        type=args.get("type"),
        content=args.get("content"),
        tags=args.get("tags"),
    ),
    check_fn=check_requirements,
    emoji="💭",
)

registry.register(
    name="broccolidb_query_graph",
    toolset="broccolidb",
    schema={
        "name": "broccolidb_query_graph",
        "description": (
            "Search the BroccoliDB Knowledge Graph by content substring with optional "
            "tag filtering. Results are ranked by epistemic confidence (sovereignty score). "
            "Use this to recall previously stored facts, decisions, or architectural rules."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "Substring query to search within node content",
                },
                "tags": {
                    "type": "string",
                    "description": "Optional comma-separated tags to filter by",
                },
                "limit": {
                    "type": "integer",
                    "description": "Maximum results to return (default 10)",
                },
            },
            "required": ["query"],
        },
    },
    handler=lambda args, **kw: broccolidb_query_graph(
        query=args.get("query"),
        tags=args.get("tags"),
        limit=args.get("limit", 10),
    ),
    check_fn=check_requirements,
    emoji="🔍",
)

registry.register(
    name="broccolidb_get_task_context",
    toolset="broccolidb",
    schema={
        "name": "broccolidb_get_task_context",
        "description": (
            "Fetch the cognitive context window for a task, resolving its "
            "knowledge dependency graph. Returns linked knowledge nodes, "
            "shared memory rules, and structural context."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "task_id": {
                    "type": "string",
                    "description": "The unique Task ID to fetch context for",
                }
            },
            "required": ["task_id"],
        },
    },
    handler=lambda args, **kw: broccolidb_get_task_context(
        task_id=args.get("task_id")
    ),
    check_fn=check_requirements,
    emoji="📋",
)

registry.register(
    name="broccolidb_append_shared_memory",
    toolset="broccolidb",
    schema={
        "name": "broccolidb_append_shared_memory",
        "description": (
            "Add a global architectural rule or guideline to the Shared Rulebook. "
            "Shared memory is visible to all agents in the workspace. Use this for "
            "cross-cutting concerns like 'all API routes must validate auth tokens'."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "memory": {
                    "type": "string",
                    "description": "The architectural guideline or rule to share globally",
                }
            },
            "required": ["memory"],
        },
    },
    handler=lambda args, **kw: broccolidb_append_shared_memory(
        memory=args.get("memory")
    ),
    check_fn=check_requirements,
    emoji="🧠",
)

registry.register(
    name="broccolidb_verify_sovereignty",
    toolset="broccolidb",
    schema={
        "name": "broccolidb_verify_sovereignty",
        "description": (
            "Perform a Skeptical Audit on a knowledge node to verify structural and "
            "epistemic sovereignty. Analyzes: git signals (commit distance, file churn), "
            "reinforcement (supporting nodes), evidence discounting, and adaptive confidence threshold."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "kb_id": {
                    "type": "string",
                    "description": "The unique Knowledge Node ID to audit",
                }
            },
            "required": ["kb_id"],
        },
    },
    handler=lambda args, **kw: broccolidb_verify_sovereignty(
        kb_id=args.get("kb_id")
    ),
    check_fn=check_requirements,
    emoji="🛡️",
)
