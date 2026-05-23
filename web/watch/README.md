# JoyZone Watch

Tamagotchi-style UI for JoyZoning orchestration: one **Synthesis Pet** mirrors run health; technical detail lives in the collapsible **Observatory** (stack trace basement).

## Dev

```bash
cd web/watch
npm install
npm run dev
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
