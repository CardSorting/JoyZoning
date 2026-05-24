# Theory Hardening Audit (Sixth Pass)

**Artifacts audited:** [whitepaper.md](whitepaper.md) v1.6 (patched from v1.5) · [research-companion.md](research-companion.md)  
**Prior audits:** [whitepaper-framework-audit.md](whitepaper-framework-audit.md) (v1.5 stabilization)  
**Date:** May 2026  
**Objective:** Pressure-test structural stability—not expand vocabulary.

---

## Executive verdict

| Question | Answer |
|----------|--------|
| Structurally stable? | **Yes, with bounded claims** — core pressures survive adversarial reading |
| Merely internally persuasive? | **Partially** — causal chain middle edges are correlational; C8 is historical not eternal |
| Ready to freeze? | **Yes (v1.6 micro-patch)** — freeze C1–C7 + B1–B4; demote C8 to constraint-persistence observation |
| Ready to operationalize? | **Yes** — as governance doctrine and measurement agenda, not as universal law |
| Still over-abstracted? | **Marginally** — “convergence” and “informational debt” are the last compressible terms; debt earns its keep as stock variable |

**Recommendation:** **FREEZE** specification at **v1.6** (causal-strength labels, scope narrowing, C8 weakened). **REVISE** research companion only where companion overstates causal necessity. **Do not** add invariants.

---

## 1. Collapse test (concept reducibility)

| Concept A | Concept B | Reducible? | Verdict |
|-----------|-----------|------------|---------|
| Informational debt | Coordination complexity | **Partially** | Debt = *stock* of accepted-vs-proposed model error; coordination complexity = *flow* of labor to converge. Debt is not redundant: two repos with equal coordination hours can differ in debt if acceptance discipline differs. **Keep C4**; clarify debt is stock, coordination is cost category (§2). |
| Reviewability | Bounded synchronization bandwidth | **Mostly yes** | C3 is review-and-acceptance bandwidth under policy. Operationally equivalent to a queueing capacity constraint. **Keep C3** as named bridge (inspectable diff unit), but subordinate theoretically to **synchronization bandwidth**. |
| Convergence | Acceptance + compositional comprehensibility | **Process vs outcomes** | Convergence = process (review → evidence → accept → compose). Not a primitive; **demote** “convergence” to taxonomy umbrella (§7), not an invariant. |
| Stable review surfaces | Coordinate persistence | **Yes** | C7 is coordinate persistence for review/acceptance. **Keep label** (operational); note equivalence in audits only. |
| Governance durability (C8) | Constraint persistence (§1.3) | **Yes** | C8 duplicates §1.3 thesis. **Demote C8** from invariant to historical observation under constraint persistence. |
| Operator opacity | Informational debt symptom | **Yes** | Opacity is terminal correlate, not primitive. Already inside C4 table. |
| Context-reconstruction cost | C7 mechanism | **Yes** | Mechanism of C7, not separate axiom. Correct as glossary term only. |
| Acceptance scarcity | C1 + C2 | **Expository merge only** | Useful narrative primitive for papers; **do not promote** to C9. C1 = rate asymmetry; C2 = state anchor. |

**Collapse outcome:** No further invariant merges beyond **C8 → constraint persistence**. Glossary may shrink by treating “convergence” as non-primitive.

---

## 2. Circularity audit

| Pair | Circular? | Grounding |
|------|-----------|-----------|
| Reviewability ↔ comprehensibility | **No** | C3: per-unit capacity pre-acceptance. C6: repo-level narrative post-composition. Different objects, different phase. |
| Convergence ↔ acceptance | **No** | Acceptance is event; convergence is ordered process including review and verification. |
| Informational debt ↔ opacity | **No** | Debt defined without opacity; opacity listed as downstream sign. |
| Stabilization ↔ convergence | **Mild overlap** | “Local stabilization” = outcome; avoid defining stabilization as “having converged” in proofs. Glossary OK if stabilization references observable checklist (§7). |
| Delivery ↔ acceptance | **No** | Delivery uses accepted state operationally (C2); not defined via comprehension. |

**Risk:** C4 defines debt as “maintaining accurate accepted-vs-proposed model” while degradation includes opacity—ensure debt metrics do not *use* opacity as input. **Rule:** measure debt via open proposals, coordinate churn, ungrounded merges—not via surveys alone.

---

## 3. Causal-strength map

Chain under test (spec §0.4):

```
propose-rate ↑ → reviewability bound stress → informational debt
  → verify without scoped confidence + acceptance without diff grounding
  → operator opacity → local/global divergence
```

| Edge | Strength | Type | Chain-breakers |
|------|----------|------|------------------|
| propose-rate ↑ → bound stress | **Strong** | Capacity / queueing | Low propose-rate; huge review staff; tiny units |
| bound stress → debt accrual | **Medium-strong** | Definitional + empirical | Strict WIP limits; immediate accept; single proposal |
| debt → symbolic verify | **Medium** | Correlational | CI gated on scoped diff; mandatory scope metadata |
| debt → acceptance without diff grounding | **Weak–medium** | Organizational | Hard merge rules; required review bots |
| debt → opacity | **Medium** | Correlational | Single operator; full-time integrator role |
| opacity → coordination dominance | **Medium** | Observational (n=1) | Small repo; low feature scope |
| opacity → C5/C6 divergence | **Medium** | Correlational | Frequent integration; architecture docs enforced |

**High propose-rate without debt?** **Yes, transiently:** if acceptance keeps pace (propose-rate ≈ accept-rate), units bounded, coordinates stable. Debt requires **lag** or **ungrounded acceptances**.

**Strongest claims (causal or quasi-definitional):**

1. Proposal throughput ≠ acceptance throughput under bounded review (C1).
2. Bound failure is capacity mismatch, not moral failure (C3).
3. Tests on wrong scope ≠ stabilization (B1).

**Weakest claims (treat as correlates until measured):**

1. Debt inevitably produces ceremonial CI.
2. Coordination always dominates implementation under mutation.
3. C8 mechanisms persist indefinitely.

---

## 4. Deepest primitive analysis

| Candidate | Verdict |
|-----------|---------|
| Bounded cognition | Too broad; background condition not specific to repos. |
| Synchronization cost | **Best general primitive** — subsumes review bandwidth (C3), gates (B3), coordinates (C7). |
| Acceptance scarcity | **Best economic primitive** — expository face of C1+C2. |
| Coordination pressure | Explanatory §2; not formal. |
| Reviewability bandwidth | Operational refinement of synchronization cost. |
| Reconstruction labor | Mechanism of C7. |
| Inspectable state alignment | **Co-primitive with acceptance scarcity** — C2 operationalized. |

**Recommended primitive pair (documentation only, not new IDs):**

1. **Acceptance scarcity** — trustworthy acceptance is rate-limited under bounded synchronization.
2. **Inspectable state alignment** — operational truth is accepted lineage on inspectable coordinates, not narrative.

All C1–C7 derive from this pair plus composition (C5) and mechanical/comprehension split (C6).

**Do not add P1/P2 invariants** — would duplicate C1/C2 without new falsifiability.

---

## 5. Acceptance scarcity vs C1

| | C1 | Acceptance scarcity |
|--|----|-----------------------|
| Content | Scaling functions differ | Economic interpretation |
| Falsifiability | Same | Same |
| Adds structure? | No | No |

**Verdict:** Use “acceptance scarcity” in **research companion and doctrine prose only**. Specification keeps **C1** as formal statement.

---

## 6. C6 attack (correctness vs comprehensibility)

### 6.1 Independence stress

| Attack | Result |
|--------|--------|
| “Green CI implies comprehension eventually” | **Fails** — comprehension is organizational model, not entailed by tests. |
| “High coverage ⇒ understandability” | **Correlates sometimes** — not identity; generated tests increase correctness without narrative. |
| “Automation removes distinction” | **Weakens over time** — summarization tools may raise comprehension *or* simulate it without alignment to accepted state. Distinction shifts to **verification of summaries vs diffs**, not eliminated. |
| “Always correlated in practice” | **Often correlated, not always** — measure both; regression acceptable for doctrine if correlation ρ<1 in samples. |

### 6.2 Operational measurability

| Comprehensibility proxy | Feasibility |
|-------------------------|-------------|
| Post-merge architecture quiz | Hard but valid |
| Inter-operator agreement on “what shipped” | Medium |
| Time-to-diagnose incident on accepted module | Medium |
| Narrative contradiction tickets | Medium |

**Verdict:** **C6 survives** as one of the **hardest-to-eliminate** claims. Wording should stay **“independent properties”** not “uncorrelated.”

---

## 7. Multi-operator stress analysis

| Concept | Single-operator | Multi-operator / fragmented |
|---------|-----------------|------------------------------|
| C1 | Strong | Strong (team attention still bounded) |
| C2 | Strong | **Weakens** if merge authority fragmented or ambiguous |
| C3 | Strong | **Requires redefinition** — distributed review bandwidth, ownership |
| C4 | Strong | **Stronger** — debt accrues via conflicting models |
| C5 | Medium | **Weakest link** — global comprehensibility is organizational, not individual |
| C6 | Strong | Strong — teams dispute narrative with green CI |
| C7 | Strong | Strong if coordinates shared; breaks if per-team coordinate schemes |
| B4 | Strong | **Amplified** — overlap on shared seams |

**Survivors:** asymmetry, debt as stock, correctness/comprehension split, coordinate persistence value.

**Needs scope footnote (spec §3):** framework optimized for **bounded supervision with resolvable merge authority**; fragmented orgs need explicit **ownership and integration roles** not specified here.

---

## 8. C8 / governance durability challenge

| Attack | Assessment |
|--------|------------|
| Future agents auto-merge with perfect tests | Shifts B2 boundary; **C4 pressures remain** if scope confidence absent |
| Diffs obsolete (semantic merge, binary artifacts) | **C2 survives** as “inspectable acceptance record,” medium may change |
| Git-era contingency | **Valid** — inspectable *lineage* persists across VCS; concrete git primitives may not |
| Merge authority eliminated | **Out of scope** per §3.2; pressures apply if *some* accountability exists |

**Verdict:** **Demote C8** from invariant to **§1.3 observation**: “Recent mutation-engine churn has not removed inspectable acceptance disciplines in observed environments.” Avoid “high durability” for merge authority—accountability is **persistent requirement**, implementation is **contingent**.

---

## 9. Historical / adjacent theory alignment

| Adjacent theory | Relationship | Novelty claim |
|-----------------|--------------|---------------|
| Distributed systems sync | Epoch gates ≈ serializing critical sections on shared narrative | **Reframing** for repo governance, not new CS |
| Two-phase commit | Propose / accept ≈ prepare / commit metaphor | **Analog only** — do not overclaim |
| Queueing theory | Reviewability bound = finite servers | **Formalizable direction** |
| Socio-technical systems | Accepted state = social + technical | **Reframing** |
| Code review literature | C3 extends review under automation load | **Emphasis shift** |
| Technical debt | Informational debt is **orthogonal stock** | **Modest extension** |
| CI/CD | B1 boundary discipline | **Clarification** |

**Genuinely useful reframing (not necessarily novel):**

- Implementation vs coordination cost split under mutation.
- C6 independence.
- C7 reconstruction cost as measurable labor.

**Avoid claiming:** “first theory of AI governance.”

---

## 10. Minimization proposal (final compression)

### 10.1 Invariant set

| Current | Proposed v1.6 | Action |
|---------|---------------|--------|
| C1 | C1 | Keep |
| C2 | C2 | Keep |
| C3 | C3 | Keep (note = sync bandwidth) |
| C4 | C4 | Keep (stock variable) |
| C5 | C5 | Keep |
| C6 | C6 | Keep |
| C7 | C7 | Keep |
| C8 | *(removed)* | Merge into §1.3 constraint persistence |
| B1–B4 | B1–B4 | Keep |

**Count:** 7 core + 4 boundaries (was 8+4).

### 10.2 Glossary compression

| Term | Action |
|------|--------|
| Convergence | Process umbrella only; remove from invariant references |
| Constraint persistence | Absorb C8 |
| Coordination complexity | Explanatory §2 only |
| Operator opacity | C4 symptom only |
| Governance durability | Delete as term; use constraint persistence |

### 10.3 Explanatory power test

Removing C4 loses degradation vocabulary. Removing C6 loses strongest falsifiable split. Removing C7 loses gate/surface prescription rationale. **Cannot go below C1–C7 without loss.**

---

## 11. Adversarial counter-models

| Counter-model | Framework weakens | Framework survives |
|---------------|-------------------|-------------------|
| Reviewability scales with AI summarization | C3 bandwidth argument | Summaries ≠ diff grounding; B2; acceptance without grounding (C4 mode) |
| Comprehensibility is socially distributed | C5 individual-operator bias | C6 team-level; debt across inconsistent local models |
| Acceptance fully automated | Human-merge prescriptions | C4 if scope not inspectable; B1 |
| Probabilistic convergence at scale | Deterministic degradation chain | C1 acceptance still rate-limited; errors become debt |
| Coordination overhead temporary | §12 observation generalization | C1 structural; tooling shifts cost type |
| Better IDEs eliminate reconstruction | C7 labor reduction | Coordinate drift still costs; gates still needed under B4 |

---

## 12. Strongest surviving claims (post-attack)

| Rank | Claim | ID |
|------|-------|-----|
| 1 | Proposal throughput ≠ trustworthy acceptance throughput under bounded sync | C1 |
| 2 | Mechanical correctness ≠ operator comprehensibility of accepted state | C6 |
| 3 | Delivery/operation must anchor on accepted inspectable state, not proposal | C2 |
| 4 | Exceeding review/sync bandwidth produces bound failure and debt accrual | C3, C4 |
| 5 | Coordinate persistence lowers reconstruction labor per acceptance | C7 |
| 6 | Local accept ≠ global compositional comprehensibility | C5 |
| 7 | Tests without scoped review do not close units | B1 |

---

## 13. Weakest claims (qualify or demote)

| Claim | Issue | Action |
|-------|-------|--------|
| C8 eternal governance | Git/accountability contingency | Demote to observation |
| Full causal chain inevitability | Middle edges organizational | Label edge strengths (§0.5) |
| Coordination dominates implementation | Single reference observation | Mark anecdotal in §12 |
| Debt recovery always expensive | Some teams fast-path small units | Soften to “often expensive” |
| B3 prescription | Normative | Keep as boundary not pressure |

---

## 14. Unresolved tensions (explicit)

1. **Individual vs collective comprehensibility** — C5/C6 need team-scale theory.
2. **Automation of acceptance** — pressures persist; framework scope excludes full autonomy.
3. **Debt measurability** — stock defined; no standard metric yet.
4. **Reviewability tooling** — summarization alters bandwidth accounting.
5. **Non-git futures** — inspectable lineage may change medium; alignment primitive survives.
6. **Correlation vs causation** — degradation modes are correlates pending longitudinal studies.

**These tensions are acceptable** for freeze if labeled open, not hidden.

---

## 15. Closure assessment

| Criterion | Status |
|-----------|--------|
| Complete enough to stop? | **Yes** — further invariants likely overfit |
| Stable enough to operationalize? | **Yes** — closure map + B boundaries sufficient |
| Minimal enough to survive? | **Yes after C8 demotion** |
| Bounded enough to avoid ideology? | **Yes** — Annex C + §3 limits hold |
| Falsifiable enough? | **Partial** — C6, C1, B1 most falsifiable; C4 modes need metrics |

| Risk | Mitigation |
|------|------------|
| Over-abstracted | Freeze vocabulary; empirical papers add metrics only |
| Under-specified multi-operator | §3 scope note |
| Implementation-shaped | Annex A quarantine maintained |

---

## 16. Research companion alignment

| Companion section | Hardening action |
|-------------------|------------------|
| §9 degradation chain | Add “correlational middle edges” footnote |
| §7 C8 durability | Soften to observed churn, not eternal |
| §12 future work | Elevate queueing formalization of C3 |
| Abstract | Replace “governance durability” with “constraint persistence” if C8 demoted |

No wholesale rewrite required.

---

## 17. Recommendation summary

| Action | Target |
|--------|--------|
| **FREEZE** | Core doctrine: C1–C7, B1–B4, closure map |
| **REVISE (v1.6 micro)** | Demote C8; add §0.5 causal strengths; narrow §3 multi-operator |
| **OPERATIONALIZE** | Metrics: accept lag, coordinate churn, ungrounded merge rate, comprehension disagreement |
| **DO NOT** | Add C9, new glossary layers, or AI philosophy sections |
| **COMPANION** | Softening per §16 |

---

## 18. v1.6 patch checklist (spec)

- [x] Remove C8 from §0.1 table; expand §1.3 constraint persistence
- [x] Add §0.5 Causal edge strengths (from §3 above)
- [x] §3.2: multi-operator / fragmented authority limitation
- [x] §12: label reference observation as anecdotal, not law
- [x] Link this audit from Annex B
- [x] Update summary + companion terminology for C8 demotion

---

*Sixth-pass audit complete. Framework resembles **synchronization and acceptance-scarcity mechanics** under inspectable repository mutation—not AI philosophy.*
