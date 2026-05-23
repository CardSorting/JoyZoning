"""
BroccoliDB Tools Package — Separation of Concerns

This package organizes BroccoliDB agent tool integrations into focused modules:
  - runner.py        → Subprocess execution layer (shared infrastructure)
  - core_tools.py    → Init, status, audit, refactor (CLI-based)
  - joyzoning_tools.py → JoyZoning-specific validation & refactoring
  - graph_tools.py   → Knowledge graph CRUD & search
  - structural_tools.py → Blast radius, entropy, cycles, integrity
"""
