# FAQ

Short answers. Deep dives link to other docs.

**Setting up for the first time?** See the [onboarding hub](onboarding/README.md) — checklists, GUI vs CLI, and plain-language status help.

---

## Onboarding & setup

### Where do I start?

[onboarding/README.md](onboarding/README.md) — pick [quickstart](onboarding/quickstart.md) (desktop) or [first-run-cli](onboarding/first-run-cli.md) (terminal).

### Do I need to read the terminal commands?

No for desktop-first setup. Use [quickstart](onboarding/quickstart.md) and the in-app **Getting Started** hub. CLI is optional: [choose-your-path](onboarding/choose-your-path.md).

### What does the health grade mean?

**Healthy / Degraded / Blocked** on Getting Started — same idea as Docker Desktop engine status. Details: [status-indicators.md](onboarding/status-indicators.md).

### Why is setup taking several minutes?

First diet-hermes install downloads Python packages (3–8 min). Later launches are seconds. [installation.md](onboarding/installation.md)

### Manager Chat does not reply

Usually missing LLM API keys on profile `joyzoning`. [api-keys-and-models.md](onboarding/api-keys-and-models.md)

### Can I skip the sample workspace?

Yes — **Project → Open Workspace** when ready. [first-run-desktop.md](onboarding/first-run-desktop.md)

### Where is the “click here not terminal” guide?

[desktop-menu-guide.md](onboarding/desktop-menu-guide.md) — menu bar and sidebar map.

### Setup failed — which doc?

[troubleshooting-setup.md](onboarding/troubleshooting-setup.md) — decision trees for app, checklist, chips, and `jz`.

### I already use Hermes chat — what changes?

[coming-from-hermes-chat.md](onboarding/coming-from-hermes-chat.md)

### Can I use Cursor instead of Hermes for a task?

**Yes.** Use `jz task start-external <task-id> --agent cursor`. JoyZoning creates the card branch and a JSDP prompt; you edit in Cursor; you run `mark-ready`, `verify`, and `complete --yes`. No Hermes lease is created. [external-agent-jsdp.md](external-agent-jsdp.md)

### Do I need Hermes for JSDP delivery chains?

**No.** Each role can be external: `jz delivery-chain next <chain-id> --external --agent cursor`. Role 2 stays blocked until Role 1 is **Complete** after your merge — same gate as managed roles.

### Why does `GET /api/tasks/{id}/lease` return 404 for my external task?

External tasks **do not have leases** by design. Use `jz task status <id>` or `GET /api/tasks/{id}/external/status`.

### Can agents mark external tasks Complete?

**No.** Same rule as managed work: only the operator runs `jz task complete <id> --yes` after review and verification.

### Where is the full managed vs external comparison?

[execution-paths.md](execution-paths.md) — decision table, cheat sheet, prerequisites.

---

## Product

### What is JoyZoning?

A **governed execution runtime for AI-assisted software work** on your machine — review, boundaries, verification, and human merge before changes count as done. Plain language: [what-is-joyzoning.md](what-is-joyzoning.md). Technical spine: [concepts.md](concepts.md).

### Is JoyZoning an IDE or a fork of Hermes?

No. It is a **supervision layer**: kanban, leases, approvals, diffs, timeline. You keep editing in your IDE; JoyZoning tracks agent work and sign-off.

### What is “1:1 workspace state”?

One **kanban card** → one **inspection context** in your **canonical workspace**. After dispatch, work is on branch `joyzoning/card-<id>`; before dispatch, you see the session workspace on your default branch. Workspace, Timeline, and git all agree. [workspace-state.md](workspace-state.md) · [philosophy.md](philosophy.md).

### Why does Workspace show a card branch vs session workspace?

**Session workspace** = the project you opened (**Project → Open Workspace**). **Card branch** = `joyzoning/card-<id>` after **Dispatch** — same folder, different branch. Always check the header before merge — like confirming the right PR branch.

### Should I trust Manager Chat or Workspace for “what changed”?

**Workspace** for files on disk; **Manager Chat** for plans and reasoning. Same split as GitHub issue comments vs the PR **Files changed** tab.

### Why “JoyZoning”?

The name reflects **zoning** agent authority: each task gets a bounded **lease** (worktree + rules), like zoning land for a specific use.

### Do I need two Hermes installations?

**No.** One diet-hermes checkout; manager and executor are different **sessions** on the same gateway. [hermes-integration.md](hermes-integration.md)

---

## Governance

### Why can’t the agent mark a task Complete?

**Merge** is the human accountability gate. Agents stop at `ready_for_review` after verification. [lease-lifecycle.md](lease-lifecycle.md)

### What is a critical card?

`risk: 3`. Dispatch and retry require `humanApprovedCritical` / `--approve-critical`. Only one critical active lease globally by default.

### What happens if verification fails?

The lease stays in **verifying** (or blocked); failed reports are stored as evidence. Re-run verify; use `supersede` when replacing a passing report. [execution-orchestration-api.md](execution-orchestration-api.md)

### Is work lost on revoke?

**No.** Worktree paths are preserved; evidence and verification JSON remain on the lease record.

---

## Technical

### What runs on which port?

| Port | Service |
|------|---------|
| 9470 | JoyZoning control plane |
| 8642 | Hermes API |
| 9119 | Hermes dashboard |

### Where is data stored?

macOS: `~/Library/Application Support/JoyZoning/joyzoning.db`. Override with `JOYZONING_DB_PATH`. [configuration.md](configuration.md)

### Desktop vs `jz` — same rules?

**Yes.** Same REST API and lease orchestration. `jz agent` is the constrained worker subset.

### Can I use JoyZoning without the desktop?

**Yes.** Run the control plane and use `jz` only. [cli.md](cli.md)

---

## Hermes

### Which Hermes repo?

[diet-hermes](https://github.com/NousResearch/hermes-agent) / Hermes Agent. JoyZoning installs via `scripts/install-diet-hermes.sh` or your own checkout.

### Why is kanban sync empty?

Usually missing dashboard token. Connect dashboard from **Hermes → Connection…**. [troubleshooting.md#hermes-and-connectivity)

### What profile does JoyZoning use?

Default profile name: **`joyzoning`** (API enabled on 8642). Configurable per session.

---

## Framework & research

### Where is the delivery-systems framework paper?

[whitepaper.md](whitepaper.md) (v1.6) — implementation-independent; JoyZoning is one embodiment (Annex A). Start with [whitepaper-summary.md](whitepaper-summary.md) for invariants **C1–C7** and boundaries **B1–B4**. Theory hardening: [theory-hardening-audit.md](theory-hardening-audit.md). Narrative: [research-companion.md](research-companion.md).

### What does “Chat plans. The repo is truth. You merge.” mean?

Framework shorthand: narrative advises; **inspectable repository state** grounds acceptance; **merge authority** is the operator accountability gate (invariant **C2**). Product mapping: [philosophy.md](philosophy.md), [execution-paths.md](execution-paths.md).

### Is the framework tied to Hermes or Cursor?

No. **Constraint persistence (§1.3):** acceptance disciplines (coordinates, merge authority, gates) have outlasted recent mutation-engine churn in observed environments—contingent mechanisms, persistent requirement. Managed and external JSDP paths are embodiments, not definitions.

---

## Contributing

### License?

MIT — [LICENSE](../LICENSE).

### How do I report bugs?

[GitHub Issues](https://github.com/CardSorting/JoyZoning/issues) with OS, .NET version, and health report if connectivity-related. [CONTRIBUTING.md](../CONTRIBUTING.md)
