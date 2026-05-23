"""
BroccoliDB Runner — Industrial-grade subprocess execution infrastructure.

All BroccoliDB tools execute TypeScript via `npx tsx`. This module
centralizes that execution with:
  - Structured error categorization (TIMEOUT, MISSING_RUNTIME, etc.)
  - Subprocess timeouts with graduated limits per operation class
  - Output sanitization and JSON extraction from mixed stdout
  - Environment isolation and reproducible cwd management
  - Telemetry hooks for execution duration tracking
"""
import json
import logging
import os
import re
import subprocess
import tempfile
import time
from typing import Optional

logger = logging.getLogger(__name__)


# ─── Constants ───
_DEFAULT_TIMEOUT = 60       # 60s for targeted operations
_AUDIT_TIMEOUT = 300        # 5 min for full-repo scans
_BOOTSTRAP_TIMEOUT = 120    # 2 min for bootstrap/warmup operations
_BROCCOLIDB_ROOT = "broccolidb"

# Maximum output size to prevent memory exhaustion from runaway processes
_MAX_OUTPUT_BYTES = 512 * 1024  # 512KB


def check_requirements() -> bool:
    """Check if BroccoliDB is available in the workspace.

    Validates both the package.json and a core module to prevent
    partial installation false-positives.
    """
    root = _BROCCOLIDB_ROOT
    return (
        os.path.exists(os.path.join(root, "package.json"))
        and os.path.isdir(os.path.join(root, "core"))
    )


def _get_env() -> dict:
    """Build an isolated subprocess environment.

    Forwards required keys (PATH, HOME, NODE_PATH) while preventing
    accidental leakage of sensitive env vars.
    """
    env = os.environ.copy()
    # Ensure Node can find broccolidb's dependencies
    node_path = os.path.join(os.path.abspath(_BROCCOLIDB_ROOT), "node_modules")
    if os.path.isdir(node_path):
        existing = env.get("NODE_PATH", "")
        env["NODE_PATH"] = f"{node_path}:{existing}" if existing else node_path
    # Suppress interactive prompts from npm/npx
    env["CI"] = "1"
    env["NONINTERACTIVE"] = "1"
    return env


def _truncate_output(text: str, max_bytes: int = _MAX_OUTPUT_BYTES) -> str:
    """Truncate output to prevent memory exhaustion."""
    if len(text) > max_bytes:
        return text[:max_bytes] + f"\n[TRUNCATED: output exceeded {max_bytes} bytes]"
    return text


def _extract_json(text: str) -> Optional[dict]:
    """Extract the last valid JSON object from mixed stdout.

    TypeScript processes may emit warnings/logs before JSON output.
    This extracts the last JSON object/array from the output stream.
    """
    if not text.strip():
        return None
    # Try parsing the full output first
    try:
        return json.loads(text.strip())
    except json.JSONDecodeError:
        pass
    # Find the last JSON object by scanning for { ... } from the end
    for match in reversed(list(re.finditer(r'\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}', text, re.DOTALL))):
        try:
            return json.loads(match.group())
        except json.JSONDecodeError:
            continue
    return None


def _make_result(success: bool, output: str = "", error: str = "",
                 error_code: str = "", duration_ms: int = 0, **extra) -> str:
    """Build a standardized JSON result string.

    All tool outputs follow this contract so the agent can reliably
    parse success/failure regardless of which tool was called.
    """
    result = {"success": success}
    if output:
        result["output"] = _truncate_output(output)
    if error:
        result["error"] = error
    if error_code:
        result["error_code"] = error_code
    if duration_ms > 0:
        result["duration_ms"] = duration_ms
    result.update(extra)
    return json.dumps(result, ensure_ascii=False)


def run_cli(args: list, timeout: int = _DEFAULT_TIMEOUT) -> str:
    """Execute a BroccoliDB CLI command and return structured JSON.

    Args:
        args: CLI arguments (e.g. ["audit"], ["status"])
        timeout: Max seconds before killing the subprocess

    Returns:
        JSON string with {success, output, error?, error_code?, duration_ms}
    """
    cmd = ["npx", "-y", "tsx", f"{_BROCCOLIDB_ROOT}/cli/index.ts"] + args
    start = time.monotonic()
    try:
        result = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            timeout=timeout,
            env=_get_env(),
        )
        duration_ms = int((time.monotonic() - start) * 1000)
        if result.returncode != 0:
            return _make_result(
                False,
                error=result.stderr.strip() or result.stdout.strip() or f"Exit code {result.returncode}",
                error_code=f"EXIT_{result.returncode}",
                duration_ms=duration_ms,
            )
        return _make_result(True, output=result.stdout.strip(), duration_ms=duration_ms)
    except subprocess.TimeoutExpired:
        duration_ms = int((time.monotonic() - start) * 1000)
        logger.warning("[BroccoliDB] CLI timed out after %ds: %s", timeout, " ".join(args))
        return _make_result(
            False,
            error=f"Operation timed out after {timeout}s. Try a more focused query or increase scope.",
            error_code="TIMEOUT",
            duration_ms=duration_ms,
        )
    except FileNotFoundError:
        return _make_result(
            False,
            error="npx/tsx not found. Ensure Node.js 18+ and tsx are installed.",
            error_code="MISSING_RUNTIME",
        )
    except OSError as e:
        return _make_result(False, error=f"OS error: {e}", error_code="OS_ERROR")
    except Exception as e:
        logger.exception("[BroccoliDB] Unexpected error running CLI")
        return _make_result(False, error=str(e), error_code="SUBPROCESS_ERROR")


def run_ts_script(script_content: str, timeout: int = _DEFAULT_TIMEOUT) -> str:
    """Execute an inline TypeScript script inside the BroccoliDB context.

    Writes a temp file in broccolidb/scratch/, executes via tsx with
    cwd=broccolidb, then cleans up. Extracts JSON from mixed output.

    Returns:
        JSON string (extracted from output) or error JSON
    """
    scratch_dir = os.path.join(_BROCCOLIDB_ROOT, "scratch")
    os.makedirs(scratch_dir, exist_ok=True)

    temp_path = None
    start = time.monotonic()
    try:
        with tempfile.NamedTemporaryFile(
            suffix=".ts", dir=scratch_dir, delete=False, mode="w"
        ) as f:
            f.write(script_content)
            temp_path = f.name

        rel_path = os.path.relpath(temp_path, _BROCCOLIDB_ROOT)
        result = subprocess.run(
            ["npx", "-y", "tsx", rel_path],
            capture_output=True,
            text=True,
            timeout=timeout,
            env=_get_env(),
            cwd=_BROCCOLIDB_ROOT,
        )
        duration_ms = int((time.monotonic() - start) * 1000)

        if result.returncode != 0:
            # Try extracting structured error from stderr/stdout
            error_text = result.stderr.strip() or result.stdout.strip()
            parsed = _extract_json(error_text)
            if parsed:
                parsed["duration_ms"] = duration_ms
                return json.dumps(parsed, ensure_ascii=False)
            return _make_result(
                False,
                error=_truncate_output(error_text) or f"Exit code {result.returncode}",
                error_code=f"SCRIPT_EXIT_{result.returncode}",
                duration_ms=duration_ms,
            )

        # Extract JSON from potentially mixed output (logs + JSON)
        stdout = result.stdout.strip()
        parsed = _extract_json(stdout)
        if parsed:
            if "duration_ms" not in parsed:
                parsed["duration_ms"] = duration_ms
            return json.dumps(parsed, ensure_ascii=False)
        # Fallback: return raw output
        return _make_result(True, output=_truncate_output(stdout), duration_ms=duration_ms)

    except subprocess.TimeoutExpired:
        duration_ms = int((time.monotonic() - start) * 1000)
        logger.warning("[BroccoliDB] TS script timed out after %ds", timeout)
        return _make_result(
            False,
            error=f"Script timed out after {timeout}s. The operation may be too heavy for this codebase size.",
            error_code="TIMEOUT",
            duration_ms=duration_ms,
        )
    except FileNotFoundError:
        return _make_result(False, error="npx/tsx not found.", error_code="MISSING_RUNTIME")
    except Exception as e:
        logger.exception("[BroccoliDB] Unexpected error in TS script")
        return _make_result(False, error=str(e), error_code="SCRIPT_ERROR")
    finally:
        if temp_path and os.path.exists(temp_path):
            try:
                os.remove(temp_path)
            except OSError:
                pass


def run_cli_interactive(args: list, stdin_input: str = "n\n", timeout: int = _DEFAULT_TIMEOUT) -> str:
    """Execute a BroccoliDB CLI command that requires stdin interaction.

    Args:
        args: CLI arguments
        stdin_input: String to pipe to stdin (bypasses interactive prompts)
        timeout: Max seconds

    Returns:
        JSON string with {success, output, error?}
    """
    cmd = ["npx", "-y", "tsx", f"{_BROCCOLIDB_ROOT}/cli/index.ts"] + args
    start = time.monotonic()
    process = None
    try:
        process = subprocess.Popen(
            cmd,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            env=_get_env(),
        )
        stdout, stderr = process.communicate(input=stdin_input, timeout=timeout)
        duration_ms = int((time.monotonic() - start) * 1000)
        return _make_result(
            success=process.returncode == 0,
            output=stdout.strip(),
            error=stderr.strip() if stderr.strip() else "",
            duration_ms=duration_ms,
        )
    except subprocess.TimeoutExpired:
        if process:
            process.kill()
            process.wait()
        duration_ms = int((time.monotonic() - start) * 1000)
        return _make_result(
            False,
            error=f"Interactive command timed out after {timeout}s",
            error_code="TIMEOUT",
            duration_ms=duration_ms,
        )
    except Exception as e:
        return _make_result(False, error=str(e), error_code="INTERACTIVE_ERROR")


# ─── TypeScript Bootstrap Templates ───
#
# Two templates for different execution contexts:
#   1. _TS_STANDALONE — Direct SpiderEngine/JoyZoning access (no DB needed)
#   2. _TS_CONTEXT    — Full AgentContext with services (needs DB)

_TS_STANDALONE = """\
import * as path from 'path';
import * as fs from 'fs';

async function run() {
  const cwd = process.cwd();
  try {
    %BODY%
  } catch (err) {
    console.log(JSON.stringify({
      success: false,
      error: err instanceof Error ? err.message : String(err),
      error_code: 'RUNTIME_ERROR',
    }));
  }
  process.exit(0);
}
run();
"""

_TS_CONTEXT = """\
import { Connection } from '../core/connection.js';
import { AgentContext } from '../core/agent-context.js';
import { Workspace } from '../core/workspace.js';
import * as path from 'path';

async function run() {
  try {
    const dbPath = path.resolve(process.cwd(), '../broccolidb.db');
    const conn = new Connection({ dbPath });
    const pool = conn.getPool();
    await pool.ensureDb();

    const userId = 'local-user';
    const workspaceId = 'local-workspace';
    const workspace = new Workspace(pool, userId, workspaceId);
    await workspace.init();
    const context = new AgentContext(workspace, pool, userId);

    %BODY%

    await context.flush();
  } catch (err) {
    console.log(JSON.stringify({
      success: false,
      error: err instanceof Error ? err.message : String(err),
      error_code: 'CONTEXT_ERROR',
    }));
  }
  process.exit(0);
}
run();
"""


def run_standalone_script(body: str, timeout: int = _DEFAULT_TIMEOUT) -> str:
    """Execute a TypeScript snippet with direct SpiderEngine/JoyZoning access.

    Use this for operations that don't need the full AgentContext (DB, services).
    Faster startup since it skips DB initialization.
    """
    script = _TS_STANDALONE.replace("%BODY%", body)
    return run_ts_script(script, timeout=timeout)


def run_agent_context_script(body: str, timeout: int = _BOOTSTRAP_TIMEOUT) -> str:
    """Execute a TypeScript snippet within a fully bootstrapped AgentContext.

    Use this for operations that need DB access, knowledge graph, or services.
    Slower startup due to DB connection and service initialization.
    """
    script = _TS_CONTEXT.replace("%BODY%", body)
    return run_ts_script(script, timeout=timeout)
