# API keys and models

JoyZoning does not sell or host AI models. **diet-hermes** calls your chosen provider (OpenRouter, Google, OpenAI, etc.) using keys you configure locally.

**When you need this:** Before **Manager Chat** can reply, or before the first **dispatch** runs an executor.

---

## Where keys live (important)

| Location | Used by |
|----------|---------|
| `~/.hermes/profiles/joyzoning/.env` | JoyZoning’s default Hermes profile |
| `~/.hermes/config.yaml` (profile dir) | Model name, toolsets |
| JoyZoning SQLite | Connection URLs, dashboard token — **not** LLM secrets |

**Never** commit `.env` files. JoyZoning repo `.env.example` is documentation only.

---

## First-time key setup (familiar wizard)

Same flow as upstream Hermes — like setting up VS Code with an extension API key:

```bash
cd /path/to/diet-hermes-main-master
.venv/bin/hermes -p joyzoning setup
```

Or interactive doctor:

```bash
.venv/bin/hermes -p joyzoning doctor
.venv/bin/hermes -p joyzoning doctor --fix
```

**You should see:** Doctor reports provider OK; Manager Chat returns a real reply.

---

## Copy keys from an existing Hermes install

If you already use Hermes globally:

1. Open `~/.hermes/.env`  
2. Copy **uncommented** lines like `OPENROUTER_API_KEY=...`  
3. Paste into `~/.hermes/profiles/joyzoning/.env`  
4. Restart gateway: `hermes -p joyzoning gateway`

---

## Pick a model

```bash
.venv/bin/hermes -p joyzoning model
# or
.venv/bin/hermes -p joyzoning config set model <provider/model-id>
```

Example already used in docs: `google/gemini-3.1-pro-preview`.

| Symptom | Likely fix |
|---------|------------|
| “Invalid API key” | Wrong profile — ensure keys in `profiles/joyzoning/.env` |
| “Model not found” | Run `hermes -p joyzoning model` to pick supported id |
| Rate limit | Switch model or provider in config |

---

## What JoyZoning needs vs Hermes

| Capability | Requires API key? |
|------------|-------------------|
| Control plane / kanban UI | No |
| Gateway health on :8642 | No (local) |
| Manager Chat | **Yes** |
| Dispatch / DietCode run | **Yes** |
| Kanban import | No (dashboard token only) |
| `jz task verify` with shell cmds | No |

---

## Desktop: errors in Manager Chat

| User-visible | Meaning |
|--------------|---------|
| Auth / 401 style message | Key missing or wrong profile |
| Timeout | Gateway down or network to provider |
| Empty stream | Check Timeline + Hermes logs `~/.hermes/profiles/joyzoning/logs/` |

**Fix path:** [hermes-setup.md](hermes-setup.md) → `hermes -p joyzoning setup` → retry message.

---

## Security habits (industry standard)

- Keys only in `.env`, not in `config.yaml` or JoyZoning appsettings  
- Profile isolation: `joyzoning` for JoyZoning work, default profile for experiments  
- Rotate keys in provider dashboard if leaked  
- **Copy health report** redacts secrets — safe for GitHub issues  

---

## Next

- [Setup checklist](setup-checklist.md) — optional milestone “First Manager Chat message”  
- [FAQ: Hermes](../faq.md#hermes)  
- [Configuration](../configuration.md)

[← Onboarding hub](README.md)
