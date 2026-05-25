# JSDP Node: 001 — Bootstrap runtime for MiniApp

## Goal

Establish minimal runnable shell for MiniApp using .NET 8.

## Scope

Complete only the work described for node `001`. Do not expand into downstream nodes.

## Dependencies

- None (root node)

## Acceptance Criteria

- Bootstrap runtime for MiniApp is implemented within declared mutation surface only.
- Verification commands pass with zero silent skips.
- Operational summary recorded for ledger append.

## Required Verification

- `echo "miniapp ok"`

## Allowed Mutation Surface

- `src/`
- `tests/`

## Stop Condition

Stop after this node.
Do not proceed to future nodes.

## Required Response Format

- Summary
- Files changed
- Verification commands run
- Result
- Recommended next action
