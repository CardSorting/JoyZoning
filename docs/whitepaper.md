# Human-Supervised Convergence Under Generative Software Mutation

*Framework specification v1.5 — embodiment Annex A*

**Version:** 1.5 (May 2026)  
**License:** MIT  
**Audit:** [whitepaper-framework-audit.md](whitepaper-framework-audit.md)  
**Summary:** [whitepaper-summary.md](whitepaper-summary.md)

---

## 0. Structural closure

### 0.1 Core invariants (C1–C8)

| ID | Invariant |
|----|-----------|
| **C1** | Mutation throughput scales with compute; convergence throughput scales with bounded human cognition. Proposing change is cheaper than accepting change. |
| **C2** | Delivery follows accepted repository state, not proposed repository state. |
| **C3** | Trustworthy acceptance requires reviewability within declared policy bounds. |
| **C4** | When propose-rate exceeds reviewability bound, informational debt accumulates; governance tends toward verification without scoped confidence and acceptance without diff grounding. |
| **C5** | Local stabilization of units does not imply repository-wide comprehensibility without compositional discipline. |
| **C6** | Repository mechanical correctness and operator comprehensibility are independent properties. |
| **C7** | Fixed review coordinates reduce context-reconstruction cost per acceptance decision. |
| **C8** | Governance mechanisms outlive mutation-engine implementations. |

### 0.2 Boundary conditions (B1–B4)

Necessary clarifications; derivative of C1–C7. Not additional axioms.

| ID | Condition |
|----|-----------|
| **B1** | Executable checks on mis-scoped or unreviewed units do not constitute local stabilization. |
| **B2** | Completion narratives do not constitute acceptance without inspectable state and merge authority. |
| **B3** | Stabilization disciplines maximize survivable convergence under load, not proposal throughput. |
| **B4** | Concurrent architectural mutation on one shared surface without synchronization gates increases informational debt accrual rate. |

### 0.3 Closure map (pressure ↔ discipline)

| Pressure (when C1 binds) | Discipline (proposed) | Failure mode addressed |
|--------------------------|----------------------|-------------------------|
| Propose-rate > review bound (C4) | Stable review coordinates (C7) | Unbounded units; coordinate drift |
| Informational debt (C4) | Merge authority (C2); convergence gates | Acceptance without comprehension |
| Verification without scope confidence (C4) | Repository truth; scoped evidence | Symbolic verification |
| Acceptance without diff grounding (C4) | Review on inspectable diff (C3) | Narrative substitution |
| Local/global divergence (C5) | Sequencing; intent artifacts | Compositional incoherence |
| Correctness without comprehensibility (C6) | Review + gates, not CI alone | Green build / opaque narrative |
| Context-reconstruction load (C7) | Fixed unit→branch→diff→evidence map | Unstable surfaces |
| Engine turnover (C8) | Supervision decoupled from engine | Governance tied to vendor |

### 0.4 Causal chain (observed)

```
propose-rate ↑ → reviewability bound stress → informational debt (C4)
  → scoped-confidence loss in verify + acceptance without diff grounding
  → operator opacity → local/global divergence (C5,C6)
```

Audits: [whitepaper-framework-audit.md](whitepaper-framework-audit.md).

---

## Executive summary

Under sufficient mutation throughput, **convergence capacity**—not mutation capacity—limits delivery. This specification defines invariants C1–C8, boundary conditions, closure mappings, and stabilization disciplines for inspectable-repository governance with bounded human supervision. Prescriptions are optional; pressures are observable.

**Chat plans. The repo is truth. You merge.**

---

## Abstract

Closed framework for mutation/convergence governance: asymmetry (C1), acceptance vs proposal (C2), reviewability (C3), informational debt (C4), local/global stabilization (C5–C6), recomputation cost (C7), governance durability (C8). Scope and non-scope explicit. Embodiment non-definitional.

---

## Glossary

| Term | Definition |
|------|------------|
| **Mutation** | Proposed repository state change. |
| **Acceptance** | Proposal enters lineage via merge authority (C2). |
| **Delivery** | Accepted state used as operational baseline (C2). |
| **Review** | Human inspection of diff on bounded unit. |
| **Verification** | Executable evidence on scoped tree. Necessary; insufficient (B1). |
| **Local stabilization** | Unit: reviewable → evidenced (if required) → accepted. |
| **Global comprehensibility** | Operators can narrate accepted repo-wide state without contradiction; requires compositional discipline (C5). |
| **Reviewability** | Unit comprehensible within policy time/attention bounds (C3). |
| **Reviewability bound failure** | Policy bounds cannot be met at current propose-rate or unit size. |
| **Informational debt** | Cumulative cost of maintaining accurate accepted-vs-proposed model (C4). |
| **Repository correctness** | Mechanical/CI satisfaction (C6). |
| **Repository comprehensibility** | Accurate operator model of accepted state (C6). |
| **Stable review surface** | Fixed coordinates: unit → branch scope → diff → evidence → acceptance (C7). |
| **Context-reconstruction cost** | Labor to re-derive scope/intent when coordinates drift (C7). |
| **Verification without scoped confidence** | Commands run; inspectable scope not established (C4). |
| **Acceptance without diff grounding** | Acceptance without inspectable diff review (C4, B2). |
| **Coordination complexity** | Cost of converging proposals into accepted, comprehensible state. |
| **Implementation complexity** | Cost of producing mechanically working artifacts. |
| **Convergence gate** | Synchronization: no new architectural epoch until local acceptance. |
| **Constraint persistence** | Reviewability and cognition bounds persist across engine generations (C1, C8). |

---

## 1. Asymmetry and constraint persistence

### 1.1 Mutation systems

A **mutation system** increases propose-rate for repository state. It does not inherently perform acceptance (C2).

*Observed under:* high propose-rate, inspectable repositories, human supervision.

### 1.2 Scaling (C1)

| Variable | Scales with |
|----------|-------------|
| Mutation throughput | Compute, automation of edit |
| Convergence throughput | Policy, attention, gate discipline |

Proposal throughput is **not** acceptance throughput. Additional proposals before acceptance **worsen** convergence state (C4, B4).

### 1.3 Constraint persistence

| Transient (high turnover) | Persistent (observed across eras) |
|---------------------------|-----------------------------------|
| Mutation-engine implementations | Bounded human reviewability |
| Edit automation substrates | Need for inspectable acceptance |
| Vendor-specific runtimes | Merge authority as accountability locus |
| | Synchronization before architectural epochs |

**C8:** Governance requirements change slowly relative to mutation substrates. Framework describes persistent constraints—not a position on any current tool.

---

## 2. Coordination vs implementation complexity

| | **Implementation complexity** | **Coordination complexity** |
|--|------------------------------|----------------------------|
| **Question** | Does the artifact work? | Is accepted state comprehensible and correctly sequenced? |
| **Primary cost** | Construction, debugging | Review, gates, supervision, context reconstruction |
| **Often reduced by** | Mutation automation | Not reduced proportionally (C1) |
| **Failure signal** | Test/build failure | Opacity, debt (C4), supervision dominating effort |

*Observed:* mutation systems lower implementation cost faster than coordination cost. Bottleneck shifts to **convergence** (reference observation §12).

This distinction explains how domain work can remain modest while supervision work dominates.

---

## 3. Scope

### 3.1 In scope

| Assumption | Framework addresses |
|------------|---------------------|
| Inspectable repository (branch, diff, logs) | C2, C7, repository truth |
| Bounded human supervision | C1, C3 |
| Governance-oriented delivery | C2, merge authority, gates |
| Software mutation environments | Entire specification |

### 3.2 Out of scope

| Not addressed | Reason |
|---------------|--------|
| Proof of program correctness | B1; verification ≠ convergence |
| Optimal team structure | Underdefined |
| Non-repository artifacts (tickets-only truth) | No inspectable diff anchor |
| Fully autonomous acceptance | Outside merge-authority model |
| Security/compliance frameworks | Adjacent; not derived here |
| Optimal propose-rate | Prescriptive tradeoff, not predicted |

### 3.3 Predictive limits

Framework predicts **degradation patterns** when propose-rate exceeds reviewability bound (C4). It does **not** predict optimal gate intervals, team size, or tool choice. Readers may reject prescriptions while accepting pressures (Annex C).

---

## 4. Informational debt (C4)

**Definition:** cumulative deficit in maintaining an accurate model of **accepted vs proposed** state. Distinct from technical debt (code structure).

| Mechanism | Effect |
|-----------|--------|
| Propose-rate > reviewability bound | Accrual per weakly closed unit |
| Unstable review coordinates (C7) | Reconstruction labor compounds accrual |
| Concurrent epochs without gates (B4) | Accrual rate increases on shared surface |
| Acceptance without comprehension | Debt persists post-merge |

| Degradation mode (C4) | Observable sign |
|----------------------|-----------------|
| Verification without scoped confidence | Verify run; branch/scope not confirmed |
| Acceptance without diff grounding | Merge without diff inspection record |
| Operator opacity | Disagreement on accepted baseline |

**Recovery:** expensive—requires re-establishing coordinates, re-review, or bounded epochs with gates (C7, B3). Debt does not amortize like some technical debt.

---

## 5. Reviewability (C3)

**Reviewability:** within declared policy, a finite reviewer can comprehend unit diff and intent in bounded time.

**Reviewability bound failure:** policy cannot be met—unit oversize, arrival overload, parallel overlap on same seams, summary substitution for diff.

Downstream per C4 chain (§0.4). Not a moral failure; a capacity mismatch.

---

## 6. Correctness and comprehensibility (C6)

| Property | Criterion |
|----------|-----------|
| **Correctness** | Mechanical/CI satisfaction |
| **Comprehensibility** | Operator can narrate accepted architecture |

Independent (C6). Correctness may hold while comprehensibility fails (B1 does not rescue comprehensibility).

---

## 7. Stabilization taxonomy

| Step | Output |
|------|--------|
| Mutation | Candidate diff |
| Review | Judgment on diff |
| Verification | Scoped evidence (B1) |
| Acceptance | Lineage update (C2) |
| Local stabilization | Review + evidence (if required) + acceptance |
| Global comprehensibility | Compositional accepted-state narrative (C5) |
| Delivery | Accepted baseline in operation (C2) |

**Global comprehensibility (C5):** continuity of accepted architectural narrative; operators can predict consequences of *accepted* changes within stated bounds—not omniscience about future proposals.

---

## 8. Stable coordinates and recomputation (C7)

**Stable review surface:** `unit → branch scope → diff set → evidence record → acceptance event`.

| Unstable | Stable |
|----------|--------|
| Coordinates shift between propose and review | Coordinates fixed per unit |
| Context re-derived from narrative each time | Context loaded from coordinates |
| Reconstruction cost dominates | Reconstruction bounded per acceptance |

**Convergence gates:** synchronization boundaries—no new architectural epoch until acceptance on stable coordinates (B3).

---

## 9. Repository truth

Acceptance grounded in inspectable state (C2, B2)—not narrative alone. Narrative advises; diffs and logs ground decisions.

---

## 10. Governance durability (C8)

*Historical observation:*

Mutation substrates (edit automation, assistants, batch runners) turnover on years-to-months cycles. Convergence practices—isolated units, diff review, scoped verification, explicit acceptance, epoch gates—persist across substrates.

| Layer | Durability |
|-------|------------|
| Mutation engines | Low |
| Reviewability bound (C1) | High |
| Merge authority (C2) | High (accountability requirement) |
| Stable coordinates (C7) | High (structural) |
| Supervision state machines | Medium (implementation-specific) |

Framework prescribes **durable layers** (truth, coordinates, authority, gates)—not engine identity.

---

## 11. Stabilization disciplines (proposed)

*When C1 binds. Disagreement on prescription does not negate C4 pressures.*

| Discipline | Invariant |
|------------|-----------|
| Repository truth | C2 |
| Stable coordinates | C7 |
| Merge authority | C2 |
| Convergence gates | B3 |
| Epoch sequencing | C5, B4 |
| Intent artifacts | C5 |

Example epoch protocol: Annex A only.

---

## 12. Reference observation

*One high propose-rate deployment (Annex A).*

Implementation effort modest; **coordination effort dominant**. Bottleneck: model of accepted vs proposed state—not artifact construction (§2). Same gates, different mutation substrate: disciplines unchanged (C8).

---

## 13. Conclusion

**C1** states the binding constraint. **C4** states predictable degradation. **C6** and **C7** separate mechanical success, comprehensibility, and reconstruction cost. **C8** states what persists.

Stabilization disciplines (§11) rise in operational value as propose-rate rises; they are not rendered obsolete by faster mutation substrates.

**C2:** Delivery follows **accepted** state, not **proposed** state.

Portable content: C1–C8, B1–B4, closure map §0.3. Not any product name.

---

**Chat plans. The repo is truth. You merge.**

---

## Annex A — Reference embodiment

Illustrative local supervision stack and epoch protocol decomposition. [jsdp.md](jsdp.md) · [execution-paths.md](execution-paths.md)

---

## Annex B — Audit record

[whitepaper-framework-audit.md](whitepaper-framework-audit.md)

---

## Annex C — Adversarial reading notes

| Position | Compatible with framework? |
|----------|---------------------------|
| Prefer maximum propose throughput | Yes; may reject B3, gates |
| Prefer autonomous acceptance | Yes; outside scope §3.2 |
| Reject human merge authority | Yes; C4 pressures may still apply if humans review |
| Different engineering culture | Pressures observable; prescriptions adaptable |
| Low mutation environments | C1 may not bind; framework low overhead |

Framework argues via **operational mechanics** (C4 correlates), not ideological alignment with supervision.
