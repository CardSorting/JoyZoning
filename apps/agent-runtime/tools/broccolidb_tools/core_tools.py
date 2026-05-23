"""
BroccoliDB Core Tools — Init, Status, Audit, Refactor.

These are the foundational CLI-driven tools that wrap the
BroccoliDB CLI commands (init, status, audit, refactor).
"""
import json
import os
from tools.registry import registry
from tools.broccolidb_tools.runner import (
    check_requirements,
    run_cli,
    run_cli_interactive,
    _AUDIT_TIMEOUT,
)


# ─── Handlers ───

def broccolidb_init(api_key: str = None, task_id: str = None) -> str:
    """Initialize and index the current repository with BroccoliDB."""
    if api_key:
        os.environ["GEMINI_API_KEY"] = api_key
    return run_cli_interactive(["init"], stdin_input="n\n", timeout=120)


def broccolidb_status(task_id: str = None) -> str:
    """View the health and stats of the Context Graph."""
    return run_cli(["status"])


def broccolidb_audit(task_id: str = None) -> str:
    """Perform a full structural audit of the codebase."""
    return run_cli(["audit"], timeout=_AUDIT_TIMEOUT)


def broccolidb_refactor(file_path: str, action: str, task_id: str = None) -> str:
    """Generate a mission-focused refactoring manifest for a file."""
    return run_cli(["refactor", file_path, action])


# ─── Registrations ───

registry.register(
    name="broccolidb_init",
    toolset="broccolidb",
    schema={
        "name": "broccolidb_init",
        "description": (
            "Initialize BroccoliDB for this repository. Creates the structural graph database, "
            "indexes all source files, and bootstraps the Context Engine. "
            "Run this ONCE at the start of a new project, or when the graph is missing/corrupt. "
            "Do NOT call this repeatedly — use broccolidb_status to check health instead."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "api_key": {
                    "type": "string",
                    "description": "Optional Gemini API Key for semantic search features",
                }
            },
        },
    },
    handler=lambda args, **kw: broccolidb_init(
        api_key=args.get("api_key"), task_id=kw.get("task_id")
    ),
    check_fn=check_requirements,
    emoji="🚀",
)

registry.register(
    name="broccolidb_status",
    toolset="broccolidb",
    schema={
        "name": "broccolidb_status",
        "description": (
            "View the health and statistics of the BroccoliDB Context Graph. "
            "Shows: node count, edge count, entropy score, last indexing time, and integrity status. "
            "Use this to verify the system is operational before running audits or queries."
        ),
        "parameters": {"type": "object", "properties": {}},
    },
    handler=lambda args, **kw: broccolidb_status(task_id=kw.get("task_id")),
    check_fn=check_requirements,
    emoji="💚",
)

registry.register(
    name="broccolidb_audit",
    toolset="broccolidb",
    schema={
        "name": "broccolidb_audit",
        "description": (
            "Perform a FULL structural audit of the entire codebase. "
            "Detects: circular dependencies, orphaned modules, high blast-radius files, "
            "monolith modules, layer leakage violations, and ghost symbols. "
            "This is an expensive operation (scans all files). For targeted analysis, "
            "use broccolidb_blast_radius or broccolidb_validate_file instead."
        ),
        "parameters": {"type": "object", "properties": {}},
    },
    handler=lambda args, **kw: broccolidb_audit(task_id=kw.get("task_id")),
    check_fn=check_requirements,
    emoji="🩺",
)

registry.register(
    name="broccolidb_refactor",
    toolset="broccolidb",
    schema={
        "name": "broccolidb_refactor",
        "description": (
            "Generate a refactoring plan for a specific file. "
            "Returns a manifest with projected health improvement, integrity score, "
            "rationale, and suggested code changes. Does NOT apply changes automatically."
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
    handler=lambda args, **kw: broccolidb_refactor(
        file_path=args.get("file_path"),
        action=args.get("action"),
        task_id=kw.get("task_id"),
    ),
    check_fn=check_requirements,
    emoji="🔨",
)
