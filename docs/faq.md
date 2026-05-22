# FAQ

Short answers. Deep dives link to other docs.

## Product

### What is JoyZoning?

A **local operator cockpit** for supervising Hermes **Manager** (planning) and **executor** (DietCode) agents on one machine. See [concepts.md](concepts.md).

### Is JoyZoning an IDE or a fork of Hermes?

No. It is a **supervision layer**: kanban, leases, approvals, diffs, timeline. You keep editing in your IDE; JoyZoning tracks agent work and sign-off.

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
