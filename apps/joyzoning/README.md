# JoyZone Watch

Tamagotchi-style UI for JoyZoning orchestration: one **Synthesis Pet** mirrors run health; technical detail lives in the collapsible **Observatory** (stack trace basement).

JoyZoning is **multi-mode** — do not collapse metaphors. See [operational-modes.md](../docs/operational-modes.md). Execution uses the **canonical workspace** (JSDP) — [philosophy.md](../docs/philosophy.md).

## Canonical shell

**`OperatorConsole`** (via `OperatorModeShell`) is the single-screen operator UI:

- One task, one selected worker, one primary action
- All critical surfaces visible without mode tabs (workers, merge/review, status, events, board)
- Server **recommended mode** is emphasis + scroll only (`ModeEmphasisBar`) — not separate screens
- Habitat / pet / campfire visuals are not shown in the production console
- **Convergence** panel + **Accept result** (not “merge”) until git convergence ships — [worker-convergence.md](../../docs/worker-convergence.md), [real-git-convergence.md](../../docs/real-git-convergence.md)

Entry points:

- `WatchApp` → `OperatorModeShell` → `OperatorConsole` (`page.tsx`)
- `WatchDashboard` / `PetShell` — legacy wrappers that delegate to the same console

**Live binding (required for legacy wrappers):** build props with `createWatchLiveBinding()` from `useLiveTask` output — never pass empty `connLabel`, missing `sessionId`, or noop handlers. `WatchDashboard` / `PetShell` accept only:

- `state: "live"` + branded `WatchLiveBinding`
- `state: "disconnected"` / `state: "preview"` for explicit non-live shells

`WatchApp` validates binding before mounting `OperatorModeShell`.

| Mode | Landing view | Canonical? |
|------|----------------|------------|
| **Planning** | `PlanningModeView` / kanban | yes |
| **Execution** | `ExecutionModeView` / workers, observatory | yes |
| **Review** | `ReviewModeView` / merge queue + approve/revoke | yes |
| **Habitat** | `HabitatModeView` / pet + optional campfire glance | no (ambient) |

`src/lib/operational-modes.ts` lists component ownership and guardrails.

## Dev

```bash
cd web/watch
npm install
npm run dev
npm test          # vitest — shell delegation + mode guardrails
npm run typecheck
```

Control plane should expose `/api/watch/bootstrap` and task live endpoints (default dev: proxied or same origin as configured in `src/lib/config.ts`).

## Pet moods

| Mood | Orchestration signal |
|------|----------------------|
| Calm | Idle / waiting |
| Focused | Active synthesis |
| Excited | High momentum |
| Confused | Unclear intent |
| Sick | Error / blocked with reason |
| Tired | Stalled / low confidence |
| Happy | Review / complete |
| Panicking | Repeated failures |

## Care meters

- **Clarity** — phase & intent legibility
- **Energy** — progress & file activity
- **Confidence** — deliverables & stability

## Actions

Feed Intent, Clarify, Rest, Retry, Review, Stabilize, Open Stack Trace — primary action is chosen from pet mood; Observatory always available for raw `blockedReason`, events, and stream lines.
