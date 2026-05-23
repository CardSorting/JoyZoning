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

JoyZoning dispatches through the **`joyzoning`** Hermes profile by default (`Hermes:Profile` in control-plane config). If you configured the model on the **default** profile (`~/.hermes/config.yaml`) instead, JoyZoning will still use a different model until you align them.

**Check what JoyZoning is using:**

```bash
jz config hermes
jz doctor   # includes hermes_model_profile check
```

**Copy your default profile model into joyzoning (recommended):**

```bash
jz config hermes sync-model --from default
# restart gateway
hermes -p joyzoning gateway
```

**Or point JoyZoning at the default profile entirely:**

```bash
jz config hermes use default
```

You can also set the profile in Hermes directly:

```bash
.venv/bin/hermes -p joyzoning model
# or
.venv/bin/hermes -p joyzoning config set model <provider/model-id>
```

| Symptom | Likely fix |
|---------|------------|
| Wrong model / provider | `jz config hermes` then `sync-model --from default` |
| “Invalid API key” (gateway / dispatch) | Stale gateway process — `curl -X POST http://127.0.0.1:9470/api/hermes/sync-credentials` or restart `hermes -p joyzoning gateway`. Key lives in `~/.hermes/profiles/joyzoning/.env` as `API_SERVER_KEY` (not the dashboard token field). |
| “Model not found” | Run `hermes -p joyzoning model` to pick supported id |
| Rate limit / 402 credits | Fix provider billing; model is correct but account is empty |

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
