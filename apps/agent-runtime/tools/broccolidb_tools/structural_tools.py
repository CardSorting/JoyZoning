"""
BroccoliDB Structural Tools — Blast radius, entropy, cycles, integrity, heal.

These tools surface the SpiderEngine's first-class structural analysis
capabilities as direct agent skills. They follow the SonarQube "Quality Gate"
pattern: each tool returns structured metrics with severity classifications
so the agent can make informed architectural decisions.

Design principles (mirroring industry standards):
  - Sourcegraph-style: Precise code intelligence via structural graph
  - SonarQube-style: Quality gates with severity (CLEAN/WARNING/CRITICAL)
  - CodeQL-style: Targeted queries over structural relationships
  - GitHub Dependency Graph: Blast radius and transitive impact analysis
"""
import json
from tools.registry import registry
from tools.broccolidb_tools.runner import (
    check_requirements,
    run_standalone_script,
    run_agent_context_script,
    _AUDIT_TIMEOUT,
    _BOOTSTRAP_TIMEOUT,
)


# ─── Handlers ───

def broccolidb_blast_radius(file_path: str, task_id: str = None) -> str:
    """Calculate the structural blast radius of a file.

    Uses the standalone SpiderEngine path (no DB needed) for fast execution.
    Returns centrality, critical dependents, coupling, and importance classification.
    """
    # Standalone — SpiderEngine + StructuralDiscovery only, no DB required
    body = f"""\
    const {{ SpiderEngine }} = await import('../core/policy/SpiderEngine.js');
    const {{ StructuralDiscoveryService }} = await import('../core/agent-context/StructuralDiscoveryService.js');

    const spider = new SpiderEngine(cwd);
    await spider.warmUp();

    const discovery = new StructuralDiscoveryService(() => spider);
    const filePath = {repr(file_path)};
    const normalizedPath = spider.normalizePath(filePath);
    const radius = discovery.getBlastRadius(filePath);
    const summary = discovery.getImportanceSummary(filePath);
    const node = spider.nodes.get(normalizedPath);

    // Compute deficiency report for the file
    const deficiencies = discovery.getDeficiencyReport(filePath);

    console.log(JSON.stringify({{
      success: true,
      filePath,
      normalizedPath,
      layer: node?.layer || 'unknown',
      totalAffectedNodes: radius.affectedNodes.length,
      centralityScore: Math.round(radius.centralityScore * 1000) / 1000,
      criticalDependents: radius.criticalDependents.slice(0, 15),
      affectedNodes: radius.affectedNodes.slice(0, 25),
      importanceSummary: summary,
      afferentCoupling: node?.afferentCoupling || 0,
      efferentCoupling: node?.efferentCoupling || 0,
      isOrphaned: node?.orphaned || false,
      lineCount: node?.lineCount || 0,
      deficiencyCount: deficiencies.length,
      severity: radius.centralityScore > 0.2 ? 'CRITICAL'
        : radius.centralityScore > 0.05 ? 'WARNING' : 'LOW',
    }}));
"""
    return run_standalone_script(body, timeout=_BOOTSTRAP_TIMEOUT)


def broccolidb_study_pack(file_path: str, task_id: str = None) -> str:
    """Generate a 'Study Pack' — the files you MUST understand before editing a target.

    Uses standalone SpiderEngine with its own graph bootstrap.
    Returns direct dependencies, critical dependents, and ambiguous symbol providers.
    """
    body = f"""\
    const {{ SpiderEngine }} = await import('../core/policy/SpiderEngine.js');
    const {{ StructuralDiscoveryService }} = await import('../core/agent-context/StructuralDiscoveryService.js');

    const spider = new SpiderEngine(cwd);
    await spider.warmUp();

    const discovery = new StructuralDiscoveryService(() => spider);
    const filePath = {repr(file_path)};
    const normalizedPath = spider.normalizePath(filePath);
    const node = spider.nodes.get(normalizedPath);
    const registry = spider.getRegistry();

    const studyItems: {{ path: string; reason: string }}[] = [];
    const seen = new Set<string>();

    if (node) {{
      // 1. Direct dependencies
      for (const resolved of Array.from(node.resolvedImports.values())) {{
        if (!seen.has(resolved as string) && resolved !== normalizedPath) {{
          seen.add(resolved as string);
          studyItems.push({{ path: resolved as string, reason: 'Direct Dependency' }});
        }}
      }}

      // 2. Critical dependents (from blast radius)
      const radius = discovery.getBlastRadius(filePath);
      for (const cr of radius.criticalDependents.slice(0, 5)) {{
        if (!seen.has(cr)) {{
          seen.add(cr);
          studyItems.push({{ path: cr, reason: 'Critical Dependent (core/ui)' }});
        }}
      }}

      // 3. Ambiguous symbol providers
      const exports = registry.getExports(normalizedPath);
      const conflicts = registry.getConflicts();
      for (const exp of exports) {{
        if (conflicts.has(exp.symbolName)) {{
          const providers = conflicts.get(exp.symbolName)!.filter((p: string) => p !== normalizedPath);
          for (const p of providers) {{
            if (!seen.has(p)) {{
              seen.add(p);
              studyItems.push({{ path: p, reason: `Ambiguity: also exports '${{exp.symbolName}}'` }});
            }}
          }}
        }}
      }}
    }}

    console.log(JSON.stringify({{
      success: true,
      filePath: normalizedPath,
      layer: node?.layer || 'unknown',
      lineCount: node?.lineCount || 0,
      studyItems: studyItems.slice(0, 25),
      totalDependencies: studyItems.length,
      importCount: node?.imports?.length || 0,
      exportCount: registry.getExports(normalizedPath).length,
    }}));
"""
    return run_standalone_script(body, timeout=_BOOTSTRAP_TIMEOUT)


def broccolidb_entropy(task_id: str = None) -> str:
    """Measure the structural entropy (health score) of the codebase.

    Uses standalone SpiderEngine for fast execution.
    Returns entropy score, coupling metrics, cycle count, and quality gate verdict.
    """
    body = """\
    const { SpiderEngine } = await import('../core/policy/SpiderEngine.js');

    const spider = new SpiderEngine(cwd);
    await spider.warmUp();

    const entropy = spider.computeEntropy();
    const coupling = spider.computeCouplingMetrics();
    const cycles = spider.detectCycles();
    const violations = spider.getViolations();

    // Quality Gate (SonarQube-style)
    const score = entropy.score;
    let qualityGate = 'PASSED';
    if (score > 0.7 || cycles.length > 5) qualityGate = 'FAILED';
    else if (score > 0.4 || cycles.length > 2) qualityGate = 'WARNING';

    console.log(JSON.stringify({
      success: true,
      entropy: {
        score: Math.round(score * 1000) / 1000,
        dimensions: entropy.dimensions || {},
      },
      coupling: coupling,
      cycleCount: cycles.length,
      cycles: cycles.slice(0, 5).map(c => c.join(' → ')),
      violationCount: violations.length,
      nodeCount: spider.nodes.size,
      qualityGate,
    }));
"""
    return run_standalone_script(body, timeout=_BOOTSTRAP_TIMEOUT)


def broccolidb_detect_cycles(task_id: str = None) -> str:
    """Detect all circular dependency chains in the codebase.

    Returns each cycle as an ordered list of file paths forming the loop,
    with severity classification and the files involved.
    """
    body = """\
    const { SpiderEngine } = await import('../core/policy/SpiderEngine.js');

    const spider = new SpiderEngine(cwd);
    await spider.warmUp();

    const cycles = spider.detectCycles();

    // Classify severity per-cycle (longer cycles = more dangerous)
    const classified = cycles.slice(0, 30).map(c => ({
      files: c,
      length: c.length,
      display: c.join(' → '),
      severity: c.length > 5 ? 'CRITICAL' : c.length > 3 ? 'HIGH' : 'MEDIUM',
    }));

    const severity = cycles.length === 0 ? 'CLEAN'
      : cycles.length < 3 ? 'WARNING'
      : cycles.length < 8 ? 'HIGH'
      : 'CRITICAL';

    console.log(JSON.stringify({
      success: true,
      totalCycles: cycles.length,
      cycles: classified,
      severity,
      recommendation: cycles.length === 0
        ? 'No circular dependencies detected. Graph is acyclic.'
        : `Found ${cycles.length} cycle(s). Use broccolidb_joyzoning_refactor with action DECOUPLE to break them.`,
    }));
"""
    return run_standalone_script(body, timeout=_BOOTSTRAP_TIMEOUT)


def broccolidb_verify_integrity(task_id: str = None) -> str:
    """Verify substrate integrity — detect drift between graph and filesystem.

    Uses standalone SpiderEngine to verify file existence for every graph node.
    Ghost nodes (deleted files still in graph) are reported for pruning.
    """
    body = """\
    const { SpiderEngine } = await import('../core/policy/SpiderEngine.js');

    const spider = new SpiderEngine(cwd);
    await spider.warmUp();

    // Check graph-to-filesystem consistency
    let ghostNodes = 0;
    const ghosts: string[] = [];
    for (const [id, node] of spider.nodes.entries()) {
      const fullPath = path.resolve(cwd, node.path);
      if (!fs.existsSync(fullPath)) {
        ghostNodes++;
        ghosts.push(node.path);
      }
    }

    // Check substrate integrity via SpiderEngine's own method
    let substrateDrift = 0;
    let substrateSynced = true;
    try {
      const check = await spider.verifySubstrateIntegrity();
      substrateDrift = check.drift;
      substrateSynced = check.synchronized;
    } catch {
      // Method may not exist in all versions
    }

    // Orphan detection: files with no imports AND no dependents
    let orphanCount = 0;
    const orphans: string[] = [];
    for (const [id, node] of spider.nodes.entries()) {
      if (node.imports.length === 0 && (node.afferentCoupling || 0) === 0) {
        orphanCount++;
        if (orphans.length < 15) orphans.push(node.path);
      }
    }

    const severity = ghostNodes > 5 || !substrateSynced ? 'CRITICAL'
      : ghostNodes > 0 ? 'WARNING' : 'CLEAN';

    console.log(JSON.stringify({
      success: true,
      ghostNodes,
      ghosts: ghosts.slice(0, 20),
      substrateSynchronized: substrateSynced,
      substrateDrift,
      orphanNodes: orphanCount,
      orphans,
      totalNodes: spider.nodes.size,
      severity,
      recommendation: ghostNodes > 0
        ? `Found ${ghostNodes} ghost node(s). Run broccolidb_heal to prune them.`
        : 'Graph integrity verified. All nodes correspond to existing files.',
    }));
"""
    return run_standalone_script(body, timeout=_BOOTSTRAP_TIMEOUT)


def broccolidb_heal(task_id: str = None) -> str:
    """Trigger graph self-healing via the AgentContext.

    Runs epistemic sunsetting: prunes stale/low-confidence knowledge nodes,
    recalculates hub scores via HITS, applies age decay, and penalizes
    high-churn evidence. Requires DB access.
    """
    body = """\
    const healResult = await context.selfHealGraph();

    // Also run structural ghost-node pruning
    const spider = context.spider;
    await spider.bootstrapGraph();
    const integrityResult = await spider.verifyGraphIntegrity(false);

    console.log(JSON.stringify({
      success: true,
      epistemic: {
        prunedNodes: healResult.prunedNodes.length,
        prunedNodeIds: healResult.prunedNodes.slice(0, 20),
        prunedEdges: healResult.prunedEdges,
      },
      structural: {
        ghostNodesPruned: integrityResult.pruned,
      },
      totalHealed: healResult.prunedNodes.length + integrityResult.pruned,
    }));
"""
    return run_agent_context_script(body, timeout=180)


def broccolidb_violations(file_path: str = None, task_id: str = None) -> str:
    """Get all structural violations, optionally filtered by file path.

    Returns SpiderEngine violations with their type, severity, and location.
    This is the structural equivalent of a linter — focused on architecture, not syntax.
    """
    filter_clause = ""
    if file_path:
        filter_clause = f"""
    const targetPath = spider.normalizePath({repr(file_path)});
    violations = violations.filter(v => v.path === targetPath || v.source === targetPath);
"""

    body = f"""\
    const {{ SpiderEngine }} = await import('../core/policy/SpiderEngine.js');

    const spider = new SpiderEngine(cwd);
    await spider.warmUp();

    let violations = spider.getViolations();
    {filter_clause}

    const grouped: Record<string, number> = {{}};
    for (const v of violations) {{
      grouped[v.type] = (grouped[v.type] || 0) + 1;
    }}

    console.log(JSON.stringify({{
      success: true,
      totalViolations: violations.length,
      violations: violations.slice(0, 30).map(v => ({{
        type: v.type,
        path: v.path,
        message: v.message,
        severity: v.severity || 'warning',
      }})),
      summary: grouped,
      qualityGate: violations.length === 0 ? 'PASSED'
        : violations.length < 5 ? 'WARNING' : 'FAILED',
    }}));
"""
    return run_standalone_script(body, timeout=_BOOTSTRAP_TIMEOUT)


# ─── Registrations ───

registry.register(
    name="broccolidb_blast_radius",
    toolset="broccolidb",
    schema={
        "name": "broccolidb_blast_radius",
        "description": (
            "Calculate the structural blast radius of a file — how many files "
            "depend on it (directly + transitively). Returns: centrality score, "
            "critical dependents (core/ui layer), coupling metrics, deficiency count, "
            "and severity classification (LOW/WARNING/CRITICAL). "
            "Use BEFORE editing high-impact files to understand downstream risk."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "file_path": {
                    "type": "string",
                    "description": "Relative path to the file to analyze",
                }
            },
            "required": ["file_path"],
        },
    },
    handler=lambda args, **kw: broccolidb_blast_radius(
        file_path=args.get("file_path"), task_id=kw.get("task_id")
    ),
    check_fn=check_requirements,
    emoji="💥",
)

registry.register(
    name="broccolidb_study_pack",
    toolset="broccolidb",
    schema={
        "name": "broccolidb_study_pack",
        "description": (
            "Generate a 'Study Pack' for a file — the prerequisite reading list "
            "you MUST understand before safely editing it. Returns: direct dependencies, "
            "critical dependents, ambiguous symbol providers, import/export counts. "
            "Use this when approaching an unfamiliar file for the first time."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "file_path": {
                    "type": "string",
                    "description": "Relative path to the file you plan to edit",
                }
            },
            "required": ["file_path"],
        },
    },
    handler=lambda args, **kw: broccolidb_study_pack(
        file_path=args.get("file_path"), task_id=kw.get("task_id")
    ),
    check_fn=check_requirements,
    emoji="📚",
)

registry.register(
    name="broccolidb_entropy",
    toolset="broccolidb",
    schema={
        "name": "broccolidb_entropy",
        "description": (
            "Measure the structural entropy (health score) of the codebase. "
            "Score: 0.0 = perfectly structured, 1.0 = chaotic. Returns: entropy "
            "dimensions, coupling metrics, cycle count, violation count, and a "
            "quality gate verdict (PASSED/WARNING/FAILED). "
            "Use to track architectural health and detect degradation."
        ),
        "parameters": {"type": "object", "properties": {}},
    },
    handler=lambda args, **kw: broccolidb_entropy(task_id=kw.get("task_id")),
    check_fn=check_requirements,
    emoji="📊",
)

registry.register(
    name="broccolidb_detect_cycles",
    toolset="broccolidb",
    schema={
        "name": "broccolidb_detect_cycles",
        "description": (
            "Detect circular dependency chains in the codebase. Each cycle is an "
            "ordered file list forming a dependency loop. Returns severity per-cycle "
            "(MEDIUM/HIGH/CRITICAL based on length) and overall grade. "
            "Use broccolidb_joyzoning_refactor with DECOUPLE to break identified cycles."
        ),
        "parameters": {"type": "object", "properties": {}},
    },
    handler=lambda args, **kw: broccolidb_detect_cycles(task_id=kw.get("task_id")),
    check_fn=check_requirements,
    emoji="🔄",
)

registry.register(
    name="broccolidb_verify_integrity",
    toolset="broccolidb",
    schema={
        "name": "broccolidb_verify_integrity",
        "description": (
            "Verify graph-to-filesystem integrity. Detects: ghost nodes (deleted files "
            "still in graph), orphan modules (zero imports + zero dependents), and "
            "substrate drift. Returns severity and actionable recommendations. "
            "Run after large deletions, branch switches, or when audit results seem stale."
        ),
        "parameters": {"type": "object", "properties": {}},
    },
    handler=lambda args, **kw: broccolidb_verify_integrity(task_id=kw.get("task_id")),
    check_fn=check_requirements,
    emoji="🔍",
)

registry.register(
    name="broccolidb_heal",
    toolset="broccolidb",
    schema={
        "name": "broccolidb_heal",
        "description": (
            "Trigger graph self-healing. Performs two sweeps: "
            "(1) Epistemic — prunes stale knowledge nodes via HITS + age decay, "
            "(2) Structural — removes ghost nodes from the SpiderEngine graph. "
            "Reports total nodes healed. Use periodically or after integrity warnings."
        ),
        "parameters": {"type": "object", "properties": {}},
    },
    handler=lambda args, **kw: broccolidb_heal(task_id=kw.get("task_id")),
    check_fn=check_requirements,
    emoji="🩹",
)

registry.register(
    name="broccolidb_violations",
    toolset="broccolidb",
    schema={
        "name": "broccolidb_violations",
        "description": (
            "List all structural violations in the codebase (or for a specific file). "
            "Returns: violation type, path, message, severity, and a summary grouped "
            "by type. Quality gate: PASSED (0 violations), WARNING (<5), FAILED (5+). "
            "This is the architectural linter — checks structure, not syntax."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "file_path": {
                    "type": "string",
                    "description": "Optional: filter violations to a specific file path",
                }
            },
        },
    },
    handler=lambda args, **kw: broccolidb_violations(
        file_path=args.get("file_path"), task_id=kw.get("task_id")
    ),
    check_fn=check_requirements,
    emoji="⚠️",
)
