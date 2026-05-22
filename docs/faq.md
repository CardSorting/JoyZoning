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

---

## Product

### What is JoyZoning?

A **local operator cockpit** for supervising Hermes **Manager** (planning) and **executor** (DietCode) agents on one machine. See [concepts.md](concepts.md).

### Is JoyZoning an IDE or a fork of Hermes?

No. It is a **supervision layer**: kanban, leases, approvals, diffs, timeline. You keep editing in your IDE; JoyZoning tracks agent work and sign-off.

### What is “1:1 workspace state”?

One **kanban card** → one **inspection folder**. After dispatch, that folder is the **lease worktree**; before dispatch, it is your **session workspace**. Workspace, Timeline, and git all agree. Plain language: [workspace-state.md](workspace-state.md).

### Why does Workspace say “Lease worktree” vs “Session workspace”?

**Session workspace** = the project you opened (**Project → Open Workspace**). **Lease worktree** = the agent’s sandbox for that card after **Dispatch**. Always check the header before merge — like confirming the right PR branch.

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

## Contributing

### License?

MIT — [LICENSE](../LICENSE).

### How do I report bugs?

[GitHub Issues](https://github.com/CardSorting/JoyZoning/issues) with OS, .NET version, and health report if connectivity-related. [CONTRIBUTING.md](../CONTRIBUTING.md)
