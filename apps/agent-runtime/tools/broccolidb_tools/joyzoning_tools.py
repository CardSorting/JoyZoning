"""
JoyZoning Tools — Architectural layer validation & targeted refactoring.

JoyZoning enforces a 5-layer architecture (domain, core, infrastructure,
plumbing, ui) with validated [LAYER: TYPE] header tags, layering constraints,
import depth limits, and architectural smell detection.

Design principles (mirroring industry patterns):
  - ArchUnit-style: Declarative architecture constraints enforceable via tooling
  - Fitness Functions (Building Evolutionary Architectures): Measurable, automated,
    layer-compliance checks that run continuously
  - Dependency Inversion Verification: domain never imports infrastructure

These tools are DISTINCT from the generic audit:
  - joyzoning_audit    → StabilityDoctor full diagnosis (metabolic + structural)
  - joyzoning_refactor → ModuleDecomposer targeted refactoring plans
  - validate_file      → Per-file JoyZoning constraint checking (fast)
  - suggest_layer      → Content-aware layer classification & tag generation
  - check_layering     → Validate import direction constraints across the whole graph
"""
import json
from tools.registry import registry
from tools.broccolidb_tools.runner import (
    check_requirements,
    run_standalone_script,
    _AUDIT_TIMEOUT,
    _BOOTSTRAP_TIMEOUT,
)


# ─── Handlers ───

def broccolidb_joyzoning_audit(task_id: str = None) -> str:
    """Perform a JoyZoning-specific forensic audit via the StabilityDoctor.

    Runs the full diagnosis pipeline: layer tag validation, geographic alignment,
    import depth violations, architectural smells, metabolic pressure, and
    structural coupling. Returns a structured report with severity.
    """
    body = """\
    const { SpiderEngine } = await import('../core/policy/SpiderEngine.js');
    const { StabilityDoctor } = await import('../core/policy/StabilityDoctor.js');

    const spider = new SpiderEngine(cwd);
    await spider.warmUp();

    const doctor = new StabilityDoctor(cwd);
    const report = await doctor.diagnose(spider);
    const entropy = spider.computeEntropy();
    const violations = spider.getViolations();

    // Classify violations by type
    const violationsByType: Record<string, number> = {};
    for (const v of violations) {
      violationsByType[v.type] = (violationsByType[v.type] || 0) + 1;
    }

    // Layer distribution
    const layerDist: Record<string, number> = {};
    for (const node of spider.nodes.values()) {
      const layer = node.layer || 'unclassified';
      layerDist[layer] = (layerDist[layer] || 0) + 1;
    }

    console.log(JSON.stringify({
      success: true,
      report: typeof report === 'string' ? report : JSON.stringify(report, null, 2),
      metrics: {
        nodeCount: spider.nodes.size,
        entropyScore: Math.round(entropy.score * 1000) / 1000,
        violationCount: violations.length,
        violationsByType,
        layerDistribution: layerDist,
      },
      qualityGate: violations.length === 0 ? 'PASSED'
        : violations.length < 5 ? 'WARNING' : 'FAILED',
    }));
"""
    return run_standalone_script(body, timeout=_AUDIT_TIMEOUT)


def broccolidb_joyzoning_refactor(file_path: str, action: str, task_id: str = None) -> str:
    """Generate a JoyZoning-aware refactoring plan using ModuleDecomposer.

    Analyzes the file against the structural graph, produces projected health
    scores, and generates actionable refactoring steps with boilerplate code.
    """
    body = f"""\
    const {{ SpiderEngine }} = await import('../core/policy/SpiderEngine.js');
    const {{ ModuleDecomposer }} = await import('../core/policy/ModuleDecomposer.js');

    const spider = new SpiderEngine(cwd);
    await spider.warmUp();

    const filePath = {repr(file_path)};
    const action = {repr(action)};
    const absPath = path.resolve(cwd, filePath);

    if (!fs.existsSync(absPath)) {{
      console.log(JSON.stringify({{ success: false, error: 'File not found: ' + filePath }}));
    }} else {{
      const content = fs.readFileSync(absPath, 'utf-8');
      const normalizedPath = spider.normalizePath(filePath);
      const node = spider.nodes.get(normalizedPath);
      const decomposer = new ModuleDecomposer();
      const plan = decomposer.analyze(filePath, content, node);

      const step = plan.steps.find(s => s.action === action);

      // Calculate current health for comparison
      const entropy = spider.computeEntropy();

      console.log(JSON.stringify({{
        success: true,
        filePath,
        action,
        currentEntropy: Math.round(entropy.score * 1000) / 1000,
        projectedHealth: plan.projectedHealth,
        integrityScore: plan.integrityScore,
        rationale: step ? step.reason : 'No specific step for this action. Consider: ' + plan.steps.map(s => s.action).join(', '),
        boilerplate: step?.boilerplate || null,
        allAvailableSteps: plan.steps.map(s => ({{ action: s.action, reason: s.reason }})),
        layer: node?.layer || 'unknown',
        lineCount: node?.lineCount || content.split('\\n').length,
      }}));
    }}
"""
    return run_standalone_script(body, timeout=_BOOTSTRAP_TIMEOUT)


def broccolidb_validate_file(file_path: str, task_id: str = None) -> str:
    """Run JoyZoning validation on a SINGLE file (fast, targeted).

    Checks: layer tag presence, geographic alignment, import depth,
    architectural smells, and layering constraints. Returns errors with
    explanations and suggested fixes.
    """
    body = f"""\
    const jz = await import('../utils/joy-zoning.js');

    const filePath = {repr(file_path)};
    const absPath = path.resolve(cwd, filePath);

    if (!fs.existsSync(absPath)) {{
      console.log(JSON.stringify({{ success: false, error: 'File not found: ' + filePath }}));
    }} else {{
      const content = fs.readFileSync(absPath, 'utf-8');
      const result = jz.validateJoyZoning(filePath, content);
      const layer = jz.getLayer(filePath, content);
      const tagSupported = jz.isLayerTagSupported(filePath, content);
      const suggestion = jz.suggestLayerForContent(content);
      const lineCount = content.split('\\n').length;

      // Classify error severity
      const classified = result.errors.map(e => ({{
        message: e,
        severity: e.includes('VIOLATION') ? 'error'
          : e.includes('SMELL') ? 'warning' : 'info',
      }}));

      const errorCount = classified.filter(e => e.severity === 'error').length;
      const warningCount = classified.filter(e => e.severity === 'warning').length;

      console.log(JSON.stringify({{
        success: result.success,
        filePath,
        currentLayer: layer,
        tagSupported,
        suggestion: suggestion ? {{ layer: suggestion.layer, reason: suggestion.reason }} : null,
        errors: classified,
        errorCount,
        warningCount,
        lineCount,
        qualityGate: errorCount === 0 && warningCount === 0 ? 'PASSED'
          : errorCount === 0 ? 'WARNING' : 'FAILED',
      }}));
    }}
"""
    return run_standalone_script(body)


def broccolidb_suggest_layer(file_path: str, task_id: str = None) -> str:
    """Analyze a file and suggest which architectural layer it belongs to.

    Uses multi-signal detection: path convention, content patterns (React/JSX → ui,
    I/O adapters → infrastructure, EventEmitter/Redux → core, ValueObject → domain),
    and existing [LAYER: TYPE] tags. Generates the correct layer comment tag.
    """
    body = f"""\
    const jz = await import('../utils/joy-zoning.js');

    const filePath = {repr(file_path)};
    const absPath = path.resolve(cwd, filePath);

    if (!fs.existsSync(absPath)) {{
      console.log(JSON.stringify({{ success: false, error: 'File not found: ' + filePath }}));
    }} else {{
      const content = fs.readFileSync(absPath, 'utf-8');
      const pathLayer = jz.getLayer(filePath);
      const contentSuggestion = jz.suggestLayerForContent(content);
      const tagLayer = jz.getLayer(filePath, content);
      const layerComment = jz.generateLayerComment(filePath, tagLayer, content);

      // Detect misalignment
      const isAligned = !contentSuggestion || contentSuggestion.layer === tagLayer;

      console.log(JSON.stringify({{
        success: true,
        filePath,
        pathBasedLayer: pathLayer,
        contentBasedLayer: contentSuggestion ? contentSuggestion.layer : null,
        contentReason: contentSuggestion ? contentSuggestion.reason : null,
        resolvedLayer: tagLayer,
        suggestedTag: layerComment ? layerComment.split('\\n')[0] : null,
        isAligned,
        recommendation: !isAligned
          ? `Geographic misalignment: content suggests '${{contentSuggestion?.layer}}' but resolved as '${{tagLayer}}'. Consider adding a [LAYER: ${{contentSuggestion?.layer?.toUpperCase()}}] tag or moving the file.`
          : `Layer '${{tagLayer}}' is consistent with content patterns.`,
      }}));
    }}
"""
    return run_standalone_script(body)


def broccolidb_check_layering(task_id: str = None) -> str:
    """Validate import direction constraints across the entire codebase.

    Enforces the JoyZoning layering rules:
      - domain → MUST NOT import infrastructure or ui
      - core   → MUST NOT import infrastructure or ui
      - ui     → CAN import core and infrastructure
      - infrastructure → CAN import core/domain (adapters pattern)

    Returns all violations with file paths and import specifiers.
    """
    body = """\
    const { SpiderEngine } = await import('../core/policy/SpiderEngine.js');
    const jz = await import('../utils/joy-zoning.js');

    const spider = new SpiderEngine(cwd);
    await spider.warmUp();

    // Layering constraint matrix
    const FORBIDDEN: Record<string, string[]> = {
      'domain': ['infrastructure', 'ui'],
      'core': ['infrastructure', 'ui'],
    };

    const violations: { source: string, sourceLayer: string, target: string, targetLayer: string, importSpec: string }[] = [];

    for (const [id, node] of spider.nodes.entries()) {
      const sourceLayer = node.layer || jz.getLayer(node.path);
      const forbidden = FORBIDDEN[sourceLayer];
      if (!forbidden) continue;

      for (const imp of node.imports) {
        const resolved = node.resolvedImports.get(imp.specifier);
        if (!resolved) continue;
        const targetNode = spider.nodes.get(resolved);
        if (!targetNode) continue;
        const targetLayer = targetNode.layer || jz.getLayer(targetNode.path);

        if (forbidden.includes(targetLayer)) {
          violations.push({
            source: node.path,
            sourceLayer,
            target: targetNode.path,
            targetLayer,
            importSpec: imp.specifier,
          });
        }
      }
    }

    console.log(JSON.stringify({
      success: true,
      totalViolations: violations.length,
      violations: violations.slice(0, 30),
      qualityGate: violations.length === 0 ? 'PASSED'
        : violations.length < 3 ? 'WARNING' : 'FAILED',
      recommendation: violations.length === 0
        ? 'All layer dependencies follow the Dependency Inversion Principle.'
        : `Found ${violations.length} layering violation(s). Use broccolidb_joyzoning_refactor with FIX_STRUCTURAL_VIOLATION to resolve.`,
    }));
"""
    return run_standalone_script(body, timeout=_BOOTSTRAP_TIMEOUT)


# ─── Registrations ───

registry.register(
    name="broccolidb_joyzoning_audit",
    toolset="broccolidb",
    schema={
        "name": "broccolidb_joyzoning_audit",
        "description": (
            "Run a JoyZoning forensic audit via the StabilityDoctor. "
            "Focuses on: [LAYER: TYPE] tag compliance, geographic alignment, import depth, "
            "architectural smells, metabolic pressure, and layer leakage. Returns structured "
            "diagnosis, violation breakdown by type, layer distribution, and quality gate."
        ),
        "parameters": {"type": "object", "properties": {}},
    },
    handler=lambda args, **kw: broccolidb_joyzoning_audit(task_id=kw.get("task_id")),
    check_fn=check_requirements,
    emoji="🏗️",
)

registry.register(
    name="broccolidb_joyzoning_refactor",
    toolset="broccolidb",
    schema={
        "name": "broccolidb_joyzoning_refactor",
        "description": (
            "Generate a JoyZoning-aware refactoring plan using the ModuleDecomposer. "
            "Returns: projected health, integrity score, rationale, suggested boilerplate, "
            "and all available refactoring steps. Use after identifying violations."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "file_path": {
                    "type": "string",
                    "description": "Relative path to the file to refactor",
                },
                "action": {
                    "type": "string",
                    "enum": [
                        "DECOMPOSE", "MOVE", "EXTRACT", "PRUNE",
                        "ALIGN_TAGS", "HEAL_STATELESSNESS",
                        "HARDEN", "DECOUPLE", "FIX_STRUCTURAL_VIOLATION",
                    ],
                    "description": "The refactoring action to perform",
                },
            },
            "required": ["file_path", "action"],
        },
    },
    handler=lambda args, **kw: broccolidb_joyzoning_refactor(
        file_path=args.get("file_path"),
        action=args.get("action"),
        task_id=kw.get("task_id"),
    ),
    check_fn=check_requirements,
    emoji="🔧",
)

registry.register(
    name="broccolidb_validate_file",
    toolset="broccolidb",
    schema={
        "name": "broccolidb_validate_file",
        "description": (
            "Run JoyZoning validation on a SINGLE file (fast, targeted). "
            "Checks: layer tag, geographic alignment, import depth, architectural smells, "
            "layering constraints. Returns classified errors (error/warning/info) and quality gate."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "file_path": {
                    "type": "string",
                    "description": "Relative path to the file to validate",
                }
            },
            "required": ["file_path"],
        },
    },
    handler=lambda args, **kw: broccolidb_validate_file(
        file_path=args.get("file_path"), task_id=kw.get("task_id")
    ),
    check_fn=check_requirements,
    emoji="✅",
)

registry.register(
    name="broccolidb_suggest_layer",
    toolset="broccolidb",
    schema={
        "name": "broccolidb_suggest_layer",
        "description": (
            "Analyze a file and suggest its architectural layer "
            "(domain, core, infrastructure, plumbing, ui). Uses multi-signal detection: "
            "path convention + content patterns + existing tags. Detects geographic misalignment "
            "and generates the correct [LAYER: TYPE] comment tag."
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
    handler=lambda args, **kw: broccolidb_suggest_layer(
        file_path=args.get("file_path"), task_id=kw.get("task_id")
    ),
    check_fn=check_requirements,
    emoji="🏷️",
)

registry.register(
    name="broccolidb_check_layering",
    toolset="broccolidb",
    schema={
        "name": "broccolidb_check_layering",
        "description": (
            "Validate import direction constraints (Dependency Inversion) across the codebase. "
            "Enforces: domain/core MUST NOT import infrastructure/ui. "
            "Returns all violations with source→target layer pairs and quality gate. "
            "This is the core JoyZoning fitness function for architectural compliance."
        ),
        "parameters": {"type": "object", "properties": {}},
    },
    handler=lambda args, **kw: broccolidb_check_layering(task_id=kw.get("task_id")),
    check_fn=check_requirements,
    emoji="🧅",
)
