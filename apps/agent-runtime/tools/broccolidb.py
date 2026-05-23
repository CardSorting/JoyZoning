"""
BroccoliDB Tool Bridge — Auto-discovery shim.

This file exists solely for compatibility with the tools/registry.py
auto-discovery mechanism (which scans tools/*.py for registry.register()
calls). The actual implementation is modularized in tools/broccolidb_tools/:

  - runner.py           → Subprocess execution infrastructure
  - core_tools.py       → Init, status, audit, refactor
  - joyzoning_tools.py  → JoyZoning validation, layer suggestion, targeted refactor
  - graph_tools.py      → Knowledge graph CRUD & search
  - structural_tools.py → Blast radius, entropy, cycles, integrity, heal
"""

# Import all submodules to trigger their registry.register() calls.
# The auto-discovery AST scanner sees the registry.register() calls
# inside these imports and loads this file at startup.
import tools.broccolidb_tools.core_tools        # noqa: F401
import tools.broccolidb_tools.joyzoning_tools    # noqa: F401
import tools.broccolidb_tools.graph_tools        # noqa: F401
import tools.broccolidb_tools.structural_tools   # noqa: F401

# Re-export check_requirements for toolset-level availability checking
from tools.broccolidb_tools.runner import check_requirements  # noqa: F401

# This dummy call ensures the AST scanner detects a top-level
# registry.register() pattern so discover_builtin_tools() imports us.
from tools.registry import registry  # noqa: F401
registry.register(
    name="_broccolidb_shim",
    toolset="broccolidb",
    schema={
        "name": "_broccolidb_shim",
        "description": "Internal shim — never exposed to agents.",
        "parameters": {"type": "object", "properties": {}},
    },
    handler=lambda args, **kw: '{"info": "BroccoliDB tools loaded via modular package"}',
    check_fn=lambda: False,  # Always hidden — check_fn returns False
)
