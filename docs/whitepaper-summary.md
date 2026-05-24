# JoyZoning Whitepaper — One-Page Summary

**Full paper:** [whitepaper.md](whitepaper.md) (v1.2)  
**Title:** *JoyZoning: Human-Supervised Delivery for Generative Software Mutation*

---

## Deepest idea

> **Generative systems increase mutation capacity faster than human systems increase convergence capacity.**

Generative coding environments are **software mutation systems**—effective at proposed change, not inherently at **convergence** (accepted, comprehensible reality).

---

## Conceptual glossary (compressed)

| Term | Meaning |
|------|---------|
| **Mutation** | Proposed repository state change (not delivery) |
| **Convergence** | Stabilizing accepted reality so an operator regains a coherent mental model—via review, evidence, merge, sequencing, bounded scope |
| **Reviewability** | Bridge between mutation and trust; systems constraint |
| **Informational debt** | Debt in maintainability of *understanding* (opaque diffs, narrative “done”, unstable stories) |
| **Cognitive stabilization** | e.g. JSDP—survivable convergence under load, not max throughput |
| **Engine churn** | Models/IDEs/runtimes change; supervision should outlive them |
| **Repository truth** | Disk inspectable; chat advisory |

**Convergence is not:** tests alone, code generation, agent consensus, prompt end.  
**Convergence is:** operator can answer *what is accepted state here?*

---

## Separate dimensions (do not collapse)

Mutation throughput · Reviewability · Verification · Convergence · Trust · Operator cognition

High mutation without reviewability → **informational debt** (faith-based merge, symbolic verify, psychological opacity).

---

## Framework vs implementation

**Framework** (durable): repository truth, reviewability, merge authority, cognitive stabilization, engine-agnostic supervision.

**JoyZoning** (embodiment): local operator cockpit implementing the framework—not the thesis itself.

**Chat plans. The repo is truth. You merge.**

**Cockpit, not engine.** Same merge gate. Different engine.

---

## JSDP

**Cognitive stabilization** for high-mutation programs—one role, one merge synchronization point, lock artifacts as reference frames. **Maximizes survivable convergence**, not mutation throughput. *Line dance, not jazz band.*

---

## TinyQuest (case study)

**Software problem:** modest app. **Mutation-management problem:** dominant—**orchestration more cognitively expensive than the domain**. Bottleneck: understanding what changed. Stabilization restored comprehensibility; external editor proved framework survives engine change.

---

## Conclusion

As mutation throughput rises, **convergence disciplines become more—not less—important.** Delivery follows accepted reality, not proposed reality.

**Chat plans. The repo is truth. You merge.**
