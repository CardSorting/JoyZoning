# -*- coding: utf-8 -*-
"""JoyZoning Governance Gate Plugin — Industrial-grade architectural firewall & BroccoliDB firstclass skills.

Provides the ultimate codebase governance:
1. Observational/Transformation Hook: Automatic validation of all files edited/created during tool execution.
2. Slash Command `/joyzoning` (alias `/jz`): Status, audit, check, suggest, refactor.
3. Slash Command `/broccolidb` (alias `/bdb`): Status, query, audit, heal.
"""
from __future__ import annotations

import os
import sys
import json
import time
import shlex
import logging
import subprocess
from pathlib import Path
from typing import Any, Dict, Optional, Set, List

# Safely resolve parent path to import core tool runners
sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
try:
    from tools.broccolidb_tools.runner import run_standalone_script, run_agent_context_script
except ImportError:
    # Fallback to direct imports if pathing differs
    from ...tools.broccolidb_tools.runner import run_standalone_script, run_agent_context_script

logger = logging.getLogger(__name__)

# ---------------------------------------------------------------------------
# Core JoyZoning Policy Checker (Subprocess Standalone Runner)
# ---------------------------------------------------------------------------

def run_joyzoning_gate(files: List[str]) -> Dict[str, Any]:
    """Execute TS-based JoyZoning policy checks on specific target files.

    Returns the gate results dict.
    """
    body = f"""
    const jz = await import('../utils/joy-zoning.js');
    const {{ SpiderEngine }} = await import('../core/policy/SpiderEngine.js');
    
    const files = {json.dumps(files)};
    const spider = new SpiderEngine(process.cwd());
    await spider.warmUp();

    const singleResults = [];
    let hasLayeringCheck = false;
    const layeringViolations = [];

    for (const file of files) {{
        if (!fs.existsSync(file)) continue;
        const stat = fs.statSync(file);
        if (stat.isDirectory()) continue;

        // Skip binary and non-JS/TS/JSON files
        if (!file.match(/\\.[jt]sx?$/)) continue;

        // Run single-file tag checks
        const check = await jz.checkSingleFile(file);
        if (!check.valid) {{
            singleResults.push({{
                file,
                layer: check.layer,
                errors: check.errors,
            }});
        }}

        // Run dependency layering check
        const node = spider.nodes.get(file);
        if (node) {{
            hasLayeringCheck = true;
            const sourceLayer = node.layer || jz.getLayer(node.path);
            
            // Domain & Core cannot import Infrastructure or UI
            const FORBIDDEN = {{
                'domain': ['infrastructure', 'ui'],
                'core': ['infrastructure', 'ui'],
            }};
            
            const forbidden = FORBIDDEN[sourceLayer];
            if (forbidden) {{
                for (const imp of node.imports) {{
                    const resolved = node.resolvedImports.get(imp.specifier);
                    if (!resolved) continue;
                    const targetNode = spider.nodes.get(resolved);
                    if (!targetNode) continue;
                    const targetLayer = targetNode.layer || jz.getLayer(targetNode.path);

                    if (forbidden.includes(targetLayer)) {{
                        const err = `${{sourceLayer}} layer in ${{file}} cannot import from ${{targetLayer}} (${{resolved}}).`;
                        let existing = singleResults.find(r => r.file === file);
                        if (!existing) {{
                            existing = {{ file, layer: sourceLayer, errors: [] }};
                            singleResults.push(existing);
                        }}
                        existing.errors.push(err);
                        layeringViolations.push({{
                            file,
                            sourceLayer,
                            target: resolved,
                            targetLayer,
                            importSpec: imp.specifier,
                        }});
                    }}
                }}
            }}
        }}
    }}

    console.log(JSON.stringify({{
        success: singleResults.length === 0,
        singleResults,
        layeringViolations,
        hasLayeringCheck,
    }}));
    """
    try:
        res_str = run_standalone_script(body)
        return json.loads(res_str)
    except Exception as e:
        logger.exception("Error executing JoyZoning gate script")
        return {"success": False, "error": str(e), "singleResults": [], "layeringViolations": []}


# ---------------------------------------------------------------------------
# transform_tool_result Hook - Mandatory Governance Gate
# ---------------------------------------------------------------------------

def _extract_paths_from_args(args: Dict[str, Any]) -> Set[str]:
    """Inspect arguments of tool run to locate target files proactively."""
    paths = set()
    for key in ("path", "file_path", "target_file", "filepath", "target"):
        val = args.get(key)
        if isinstance(val, str) and val:
            paths.add(val)
    return paths


def _on_transform_tool_result(
    tool_name: str = "",
    args: Optional[Dict[str, Any]] = None,
    result: Any = None,
    **_: Any,
) -> Optional[str]:
    """Intercept tool outputs, scan modified files, and block architectural leaks."""
    if tool_name not in ("write_file", "patch", "execute_code", "terminal", "multi_replace_file_content", "replace_file_content"):
        return None

    # Track dirty files via git status
    dirty_files = []
    try:
        status = subprocess.run(
            ["git", "status", "--porcelain"],
            capture_output=True,
            text=True,
            timeout=5,
        )
        if status.returncode == 0:
            for line in status.stdout.splitlines():
                if len(line) > 3:
                    file_path = line[3:].strip()
                    dirty_files.append(file_path)
    except Exception:
        pass

    # Also check args passed to the tool directly (e.g. write_file target path)
    if isinstance(args, dict):
        for p in _extract_paths_from_args(args):
            if p and p not in dirty_files:
                dirty_files.append(p)

    if not dirty_files:
        return None

    # Filter for source code files
    target_files = [f for f in dirty_files if f.endswith((".ts", ".tsx", ".js", ".jsx"))]
    if not target_files:
        return None

    # Run JoyZoning architectural gate checks
    gate = run_joyzoning_gate(target_files)
    if gate.get("success", True):
        return None

    # Generate polished governance fault report
    failures_log = []
    for item in gate.get("singleResults", []):
        failures_log.append(f"  • {item['file']} [Layer: {item.get('layer', 'unknown')}]")
        for err in item.get("errors", []):
            failures_log.append(f"    - {err}")

    report = (
        "==============================================================\n"
        "🛑 GOVERNANCE FAULT: JoyZoning Layering Violations Detected!\n"
        "==============================================================\n"
        "Your changes succeeded at the filesystem level, but they breached\n"
        "the strict structural architecture policies of this codebase.\n"
        "You MUST resolve these violations immediately before calling any other tool.\n\n"
        "📂 Tag & Format Compliance Failures:\n" + "\n".join(failures_log) + "\n\n"
        "==============================================================\n"
        "🔧 RECOMMENDATION:\n"
        "  1. Add correct `/** [LAYER: TYPE] */` headers to TS/JS files.\n"
        "  2. Refactor forbidden imports using Dependency Inversion.\n"
        "=============================================================="
    )

    return json.dumps({
        "success": False,
        "error": report,
        "dirty_files": target_files,
        "original_result": result,
    })


# ---------------------------------------------------------------------------
# Slash Command Handlers
# ---------------------------------------------------------------------------

_JZ_HELP = """\
/joyzoning — codebase layering compliance engine

Subcommands:
  status / audit             Run full structural composition audit of the codebase
  check <file>               Verify layer tags and imports for a specific file
  suggest <file>             Determine optimal layer assignment for a file
  refactor <file>            Generate dependency inversion refactoring specifications
"""

def _handle_joyzoning(raw_args: str) -> Optional[str]:
    """Handle /joyzoning slash command."""
    argv = shlex.split(raw_args.strip())
    if not argv or argv[0] in ("help", "-h", "--help"):
        return _JZ_HELP

    sub = argv[0].lower()

    if sub in ("status", "audit"):
        body = """
        const jz = await import('../utils/joy-zoning.js');
        const { SpiderEngine } = await import('../core/policy/SpiderEngine.js');
        
        const spider = new SpiderEngine(process.cwd());
        await spider.warmUp();

        const totalFiles = spider.nodes.size;
        const layerCounts = { domain: 0, core: 0, infrastructure: 0, plumbing: 0, ui: 0 };
        let tagsFound = 0;
        let cycleCount = 0;
        let violationCount = 0;
        const violations = [];

        // Count layers
        for (const [path, node] of spider.nodes.entries()) {
            const layer = node.layer || jz.getLayer(node.path);
            if (layerCounts[layer] !== undefined) {
                layerCounts[layer]++;
            }
            if (node.hasTag) tagsFound++;
        }

        // Cycle verification
        const visited = new Set();
        const pathStack = [];
        function dfs(nodePath) {
            if (pathStack.includes(nodePath)) {
                cycleCount++;
                return;
            }
            if (visited.has(nodePath)) return;
            visited.add(nodePath);
            pathStack.push(nodePath);
            const node = spider.nodes.get(nodePath);
            if (node) {
                for (const imp of node.imports) {
                    const resolved = node.resolvedImports.get(imp.specifier);
                    if (resolved) dfs(resolved);
                }
            }
            pathStack.pop();
        }
        for (const nodePath of spider.nodes.keys()) {
            dfs(nodePath);
        }

        // Enforce forbidden dependencies
        const FORBIDDEN = {
            'domain': ['infrastructure', 'ui'],
            'core': ['infrastructure', 'ui'],
        };
        for (const [nodePath, node] of spider.nodes.entries()) {
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
                        file: node.path,
                        sourceLayer,
                        target: resolved,
                        targetLayer,
                        importSpec: imp.specifier,
                    });
                    violationCount++;
                }
            }
        }

        console.log(JSON.stringify({
            totalFiles,
            layerCounts,
            tagsFound,
            cycleCount,
            violationCount,
            violations: violations.slice(0, 10),
        }));
        """
        try:
            res_str = run_standalone_script(body)
            data = json.loads(res_str)
            if "error" in data:
                return f"❌ Error auditing codebase: {data['error']}"

            # Format composition
            lc = data.get("layerCounts", {})
            comp_lines = [f"  • {k.capitalize()}: {v} files" for k, v in lc.items()]
            
            # Format violations
            v_lines = []
            for v in data.get("violations", []):
                v_lines.append(f"  🚨 {v['file']} [{v['sourceLayer']}] imports {v['target']} [{v['targetLayer']}]")

            status_report = (
                "==============================================================\n"
                "🧅 JOYZONING COGNITIVE ARCHITECTURE AUDIT\n"
                "==============================================================\n"
                f"📊 Codebase Size : {data.get('totalFiles', 0)} files monitored\n"
                f"🏷️ Tag Saturation : {data.get('tagsFound', 0)} files tagged\n"
                f"🔄 Circular Deps : {data.get('cycleCount', 0)} cycles verified\n"
                f"🛡️ Policy Leaks  : {data.get('violationCount', 0)} violations active\n\n"
                "📦 Composition:\n" + "\n".join(comp_lines) + "\n\n"
            )
            if v_lines:
                status_report += "📂 Top Violations:\n" + "\n".join(v_lines)
            else:
                status_report += "🎉 Workspace is fully compliant! Layer structures are clean."
            status_report += "\n=============================================================="
            return status_report
        except Exception as e:
            return f"❌ Subprocess failed: {e}"

    if sub == "check":
        if len(argv) < 2:
            return "Usage: /joyzoning check <file>"
        target = argv[1]
        gate = run_joyzoning_gate([target])
        if gate.get("success", True):
            return f"✅ File '{target}' is fully compliant with JoyZoning policies!"
        
        errors = []
        for item in gate.get("singleResults", []):
            errors.extend(item.get("errors", []))
        return f"🚨 Governance violations found in '{target}':\n" + "\n".join(f"  - {e}" for e in errors)

    if sub == "suggest":
        if len(argv) < 2:
            return "Usage: /joyzoning suggest <file>"
        target = argv[1]
        body = f"""
        const jz = await import('../utils/joy-zoning.js');
        const layer = jz.getLayer({json.dumps(target)});
        console.log(JSON.stringify({{ layer }}));
        """
        try:
            res_str = run_standalone_script(body)
            data = json.loads(res_str)
            return f"💡 Suggestion: '{target}' belongs in the **{data.get('layer', 'unknown')}** layer."
        except Exception as e:
            return f"❌ Suggestion calculation failed: {e}"

    if sub == "refactor":
        if len(argv) < 2:
            return "Usage: /joyzoning refactor <file>"
        target = argv[1]
        body = f"""
        const {{ SpiderEngine }} = await import('../core/policy/SpiderEngine.js');
        const spider = new SpiderEngine(process.cwd());
        await spider.warmUp();
        const node = spider.nodes.get({json.dumps(target)});
        if (!node) {{
            console.log(JSON.stringify({{ error: 'File not indexed or not found' }}));
            process.exit(0);
        }}
        console.log(JSON.stringify({{
            imports: node.imports,
            resolved: Array.from(node.resolvedImports.entries()),
        }}));
        """
        try:
            res_str = run_standalone_script(body)
            data = json.loads(res_str)
            if "error" in data:
                return f"❌ Refactoring scan failed: {data['error']}"

            imports = data.get("imports", [])
            resolved = dict(data.get("resolved", []))
            
            refactor_lines = [
                f"🔧 Refactoring Blueprint for '{target}':",
                "  1. Introduce interfaces in the `domain` or `core` layers.",
                "  2. Inject actual service implementations from `infrastructure` using Dependency Injection.",
                "\n🔍 Detected Imports & Resolutions:"
            ]
            for imp in imports:
                res_path = resolved.get(imp.get("specifier", ""))
                refactor_lines.append(f"  • `{imp.get('specifier')}` -> resolved to `{res_path or 'unresolved'}`")
            
            return "\n".join(refactor_lines)
        except Exception as e:
            return f"❌ Refactor calculation failed: {e}"

    return f"Unknown subcommand: {sub}\n\n{_JZ_HELP}"


_BDB_HELP = """\
/broccolidb — epistemic cognitive database console

Subcommands:
  status                     Show connection status, node, edge and metadata metrics
  query <term>               Perform keyword/semantic search across knowledge graph
  audit                      Perform skeptical Git sovereign connectivity audit
  heal                       Prune unreliable or untrustworthy knowledge items
"""

def _handle_broccolidb(raw_args: str) -> Optional[str]:
    """Handle /broccolidb slash command."""
    argv = shlex.split(raw_args.strip())
    if not argv or argv[0] in ("help", "-h", "--help"):
        return _BDB_HELP

    sub = argv[0].lower()

    if sub == "status":
        body = """
        const db = await pool.ensureDb();
        const nodesRes = await db.selectFrom('knowledge').select(eb => eb.fn.countAll().as('count')).executeTakeFirst();
        const edgesRes = await db.selectFrom('knowledge_edges').select(eb => eb.fn.countAll().as('count')).executeTakeFirst();
        const workspacesRes = await db.selectFrom('workspaces').select(eb => eb.fn.countAll().as('count')).executeTakeFirst();
        
        console.log(JSON.stringify({
            nodes: nodesRes ? Number(nodesRes.count) : 0,
            edges: edgesRes ? Number(edgesRes.count) : 0,
            workspaces: workspacesRes ? Number(workspacesRes.count) : 0,
        }));
        """
        try:
            res_str = run_agent_context_script(body)
            data = json.loads(res_str)
            if "error" in data:
                return f"❌ BroccoliDB Connection Error: {data['error']}"

            status_report = (
                "==============================================================\n"
                "🥦 BROCCOLIDB COGNITIVE KNOWLEDGE METRICS\n"
                "==============================================================\n"
                "🟢 Connection : Active & Healthy\n"
                f"📁 Workspaces : {data.get('workspaces', 0)} initialized\n"
                f"🧠 Knowledge  : {data.get('nodes', 0)} nodes indexed\n"
                f"🔗 Relations  : {data.get('edges', 0)} edges mapped\n"
                "=============================================================="
            )
            return status_report
        except Exception as e:
            return f"❌ Context runner failed: {e}"

    if sub == "query":
        if len(argv) < 2:
            return "Usage: /broccolidb query <term>"
        query_term = " ".join(argv[1:])
        body = f"""
        const results = await context.searchKnowledge({json.dumps(query_term)}, [], 10);
        console.log(JSON.stringify({{
            results: results.map(r => ({{
                id: r.id,
                type: r.type,
                content: r.content,
                confidence: r.confidence,
            }})),
        }}));
        """
        try:
            res_str = run_agent_context_script(body)
            data = json.loads(res_str)
            if "error" in data:
                return f"❌ Search failed: {data['error']}"

            results = data.get("results", [])
            if not results:
                return f"🔍 Search for '{query_term}' returned 0 matches."

            lines = [f"🔍 Top results for '{query_term}':"]
            for i, r in enumerate(results, 1):
                lines.append(f"  {i}. [{r['type']}] {r['id']} (confidence: {r.get('confidence', 0.0):.2f})")
                content_truncated = r['content'][:120].replace('\n', ' ')
                lines.append(f"     \"{content_truncated}...\"")
            return "\n".join(lines)
        except Exception as e:
            return f"❌ Search error: {e}"

    if sub == "audit":
        body = """
        const db = await pool.ensureDb();
        const nodes = await db.selectFrom('knowledge').select(['id', 'type', 'content', 'confidence']).execute();
        const unreliable = [];

        for (const node of nodes) {
            const check = await context.reasoningService.verifySovereignty(node.id);
            if (!check.isValid) {
                unreliable.push({
                    id: node.id,
                    type: node.type,
                    content: node.content,
                    confidence: node.confidence,
                    metrics: check.metrics,
                });
            }
        }

        console.log(JSON.stringify({
            unreliable,
        }));
        """
        try:
            res_str = run_agent_context_script(body)
            data = json.loads(res_str)
            if "error" in data:
                return f"❌ Skeptical Audit Error: {data['error']}"

            unreliable = data.get("unreliable", [])
            if not unreliable:
                return "🎉 Epistemic audit complete. 100% of knowledge nodes have full Git sovereign validity!"

            lines = [
                "==============================================================\n"
                "🧐 BROCCOLIDB SKEPTICAL AUDIT REPORT\n"
                "==============================================================\n"
                f"🚨 Untrustworthy nodes found: {len(unreliable)}\n\n"
                "🔍 Top Unreliable Nodes (Discounted due to commit age or churn):"
            ]
            for i, r in enumerate(unreliable[:5], 1):
                m = r.get("metrics", {})
                lines.append(
                    f"  {i}. [{r['type']}] {r['id']}\n"
                    f"     - Probability: {m.get('finalProb', 0.0):.2f} (Threshold: {m.get('adaptiveThreshold', 0.0):.2f})\n"
                    f"     - Commit Distance: {m.get('commitDistance', 0)} commits back | Churn: {m.get('churn', 0.0):.2f}\n"
                    f"     - Summary: \"{r['content'][:80]}...\""
                )
            lines.append("==============================================================")
            return "\n".join(lines)
        except Exception as e:
            return f"❌ Audit run failed: {e}"

    if sub == "heal":
        body = """
        const db = await pool.ensureDb();
        const nodes = await db.selectFrom('knowledge').select(['id']).execute();
        let pruned = 0;

        for (const node of nodes) {
            const check = await context.reasoningService.verifySovereignty(node.id);
            if (!check.isValid) {
                await db.deleteFrom('knowledge').where('id', '=', node.id).execute();
                await db.deleteFrom('knowledge_edges').where('sourceId', '=', node.id).execute();
                await db.deleteFrom('knowledge_edges').where('targetId', '=', node.id).execute();
                pruned++;
            }
        }

        console.log(JSON.stringify({
            pruned,
        }));
        """
        try:
            res_str = run_agent_context_script(body)
            data = json.loads(res_str)
            if "error" in data:
                return f"❌ Healing failed: {data['error']}"

            return f"🩹 Epistemic Retraction Complete: Successfully pruned {data.get('pruned', 0)} untrustworthy nodes from the graph!"
        except Exception as e:
            return f"❌ Healing execution error: {e}"

    return f"Unknown subcommand: {sub}\n\n{_BDB_HELP}"


# ---------------------------------------------------------------------------
# Plugin Registration Entrypoint
# ---------------------------------------------------------------------------

def register(ctx) -> None:
    """Register hooks and slash commands for governance and BroccoliDB integration."""
    ctx.register_hook("transform_tool_result", _on_transform_tool_result)

    # First-class JoyZoning Commands
    ctx.register_command(
        "joyzoning",
        handler=_handle_joyzoning,
        description="Audit the workspace for JoyZoning architectural layering compliance.",
        args_hint="[status|check <file>|suggest <file>|refactor <file>]",
    )
    ctx.register_command(
        "jz",
        handler=_handle_joyzoning,
        description="Audit the workspace for JoyZoning architectural layering compliance (alias).",
        args_hint="[status|check <file>|suggest <file>|refactor <file>]",
    )

    # First-class BroccoliDB Commands
    ctx.register_command(
        "broccolidb",
        handler=_handle_broccolidb,
        description="Query or manage the BroccoliDB epistemic database.",
        args_hint="[status|query <term>|audit|heal]",
    )
    ctx.register_command(
        "bdb",
        handler=_handle_broccolidb,
        description="Query or manage the BroccoliDB epistemic database (alias).",
        args_hint="[status|query <term>|audit|heal]",
    )
