# Framework Summary (v1.5)

**Paper:** [whitepaper.md](whitepaper.md) · **Audit:** [whitepaper-framework-audit.md](whitepaper-framework-audit.md)

---

## Core invariants (C1–C8)

| ID | Statement |
|----|-----------|
| **C1** | Mutation ∝ compute; convergence ∝ bounded cognition. Propose cheaper than accept. |
| **C2** | Delivery follows **accepted** state, not proposed. |
| **C3** | Trustworthy acceptance requires reviewability within policy bounds. |
| **C4** | Propose-rate > review bound → informational debt; verify without scoped confidence; acceptance without diff grounding. |
| **C5** | Local stabilization ≠ global comprehensibility without composition. |
| **C6** | Correctness ≠ comprehensibility. |
| **C7** | Fixed review coordinates reduce context-reconstruction cost. |
| **C8** | Governance outlives mutation engines. |

**Boundaries:** B1 tests ≠ stabilization · B2 narrative ≠ acceptance · B3 maximize convergence not propose-rate · B4 concurrent epochs accelerate debt.

---

## Closure map (pressure → discipline)

Propose overload → stable coordinates · debt → merge authority + gates · opacity → repository truth + review · engine churn → supervision decoupling (§0.3).

---

## Coordination vs implementation (§2)

Mutation lowers **implementation** cost faster than **coordination** cost. Bottleneck shifts to convergence under load.

---

## Key models

| Model | Point |
|-------|--------|
| **Informational debt (C4)** | Cost of maintaining accepted-vs-proposed model; expensive recovery |
| **Reviewability bound failure** | Policy cannot be met at current load |
| **Stable coordinates (C7)** | unit → branch → diff → evidence → accept |
| **Constraint persistence** | Cognition/review bounds persist; engines turn over |

---

## Scope

In: inspectable repo, bounded supervision, mutation governance.  
Out: correctness proofs, team design, ticket-only truth, full autonomy.  
Prescription optional; pressures observable (Annex C).

---

## Reference observation

Modest implementation; **coordination dominated**. Annex A = embodiment only.

---

**Chat plans. The repo is truth. You merge.**
