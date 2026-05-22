# 5-minute quickstart (desktop)

**Goal:** Open JoyZoning and land on **Manager Chat** with a green health grade.

**Audience:** New operators — minimal terminal use.

**Time:** ~5 minutes if diet-hermes is already built; ~15 minutes on first install (downloads Python packages).

---

## Before you begin

| You need | Why |
|----------|-----|
| **macOS** (primary) or Linux | Desktop `.app` targets macOS arm64; control plane runs on both |
| **.NET 8 SDK** | Runs JoyZoning — [install](https://dotnet.microsoft.com/download) if `dotnet --version` shows 6.x |
| **Internet** (first run only) | Installs diet-hermes Python dependencies |
| **LLM API key** (optional for step 1) | Required before Manager Chat can reply — [api-keys-and-models.md](api-keys-and-models.md) |

You do **not** need two copies of Hermes. JoyZoning uses **one** diet-hermes install.

---

## Steps

### 1. Get the code

```bash
git clone https://github.com/CardSorting/JoyZoning.git
cd JoyZoning
```

### 2. Start the app

```bash
./scripts/run-dev.sh
```

**You should see:** A window opens; briefly a **“Setting up JoyZoning…”** overlay, then **Manager Chat** or **Getting Started**.

If the overlay runs long (3–8 minutes), that is normal on first install — it is building diet-hermes.

### 3. Glance at health

Open **Getting Started** (menu: **Settings → Open Getting Started**) if you are not already there.

| Grade | Meaning |
|-------|---------|
| **Healthy** | Core checklist complete — proceed |
| **Degraded** | Something optional failed — you can still explore |
| **Blocked** | Fix items in the checklist before dispatch |

Chip colors in the status bar: [status-indicators.md](status-indicators.md).

### 4. Open your project (when ready)

**Project → Open Workspace** → choose your repo folder.

Until then, JoyZoning may use a **sample workspace** under Application Support — fine for exploring.

### 5. Send one planning message (optional)

**Manager Chat** → type a short goal (e.g. “List three tasks to add README badges”).

**You should see:** Streaming reply text. If you get an API error, add keys: [api-keys-and-models.md](api-keys-and-models.md).

---

## What *not* to worry about yet

- **Merge** and **verification** — come after your first dispatch ([whats-next.md](whats-next.md))
- **Critical risk cards** — advanced governance; default tasks are low risk
- **Terminal / `jz`** — optional; same rules as the desktop ([choose-your-path.md](choose-your-path.md))

---

## Something wrong?

| Symptom | Fix |
|---------|-----|
| Red **API** chip | **Hermes → Ensure Gateway** or **Getting Started → Run smart setup** |
| Red **Dashboard** chip | **Hermes → Connection… → Connect dashboard** |
| App won’t open | [installation.md](installation.md) · [.NET 8 on PATH](installation.md#net-8-on-macos) |
| Stuck on setup overlay | Wait; then **Smart setup** from Getting Started |

Follow the trees: [troubleshooting-setup.md](troubleshooting-setup.md) · [troubleshooting.md](../troubleshooting.md).

---

## Next steps

- [First run (desktop) — full detail](first-run-desktop.md)
- [Setup checklist](setup-checklist.md)
- [What to do after setup](whats-next.md)

[← Onboarding hub](README.md)
