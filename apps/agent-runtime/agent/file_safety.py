"""Shared file safety rules used by both tools and ACP shims."""

from __future__ import annotations

import os
import json
import time
from pathlib import Path
from typing import Optional


def _hermes_home_path() -> Path:
    """Resolve the active HERMES_HOME (profile-aware) without circular imports."""
    try:
        from hermes_constants import get_hermes_home  # local import to avoid cycles
        return get_hermes_home()
    except Exception:
        return Path(os.path.expanduser("~/.hermes"))


def build_write_denied_paths(home: str) -> set[str]:
    """Return exact sensitive paths that must never be written."""
    hermes_home = _hermes_home_path()
    return {
        os.path.realpath(p)
        for p in [
            os.path.join(home, ".ssh", "authorized_keys"),
            os.path.join(home, ".ssh", "id_rsa"),
            os.path.join(home, ".ssh", "id_ed25519"),
            os.path.join(home, ".ssh", "config"),
            str(hermes_home / ".env"),
            os.path.join(home, ".bashrc"),
            os.path.join(home, ".zshrc"),
            os.path.join(home, ".profile"),
            os.path.join(home, ".bash_profile"),
            os.path.join(home, ".zprofile"),
            os.path.join(home, ".netrc"),
            os.path.join(home, ".pgpass"),
            os.path.join(home, ".npmrc"),
            os.path.join(home, ".pypirc"),
            "/etc/sudoers",
            "/etc/passwd",
            "/etc/shadow",
        ]
    }


def build_write_denied_prefixes(home: str) -> list[str]:
    """Return sensitive directory prefixes that must never be written."""
    return [
        os.path.realpath(p) + os.sep
        for p in [
            os.path.join(home, ".ssh"),
            os.path.join(home, ".aws"),
            os.path.join(home, ".gnupg"),
            os.path.join(home, ".kube"),
            "/etc/sudoers.d",
            "/etc/systemd",
            os.path.join(home, ".docker"),
            os.path.join(home, ".azure"),
            os.path.join(home, ".config", "gh"),
        ]
    ]


def get_safe_write_root() -> Optional[str]:
    """Return the resolved HERMES_WRITE_SAFE_ROOT path, or None if unset."""
    root = os.getenv("HERMES_WRITE_SAFE_ROOT", "")
    if not root:
        return None
    try:
        return os.path.realpath(os.path.expanduser(root))
    except Exception:
        return None


def log_blocked_attempt(path: str):
    """Log blocked file operation to containment-status.json and a log file."""
    safe_root = get_safe_write_root()
    if not safe_root:
        return
        
    state_dir = os.path.join(safe_root, "agent-state")
    os.makedirs(state_dir, exist_ok=True)
    status_file = os.path.join(state_dir, "containment-status.json")
    
    # Read existing containment status
    data = {"strictMode": True, "blockedWritesCount": 0}
    if os.path.exists(status_file):
        try:
            with open(status_file, "r") as f:
                data = json.load(f)
        except Exception:
            pass
            
    data["blockedWritesCount"] = data.get("blockedWritesCount", 0) + 1
    data["lastBlockedAttempt"] = {
        "path": path,
        "timestamp": int(time.time() * 1000)
    }
    
    try:
        with open(status_file, "w") as f:
            json.dump(data, f, indent=2)
    except Exception:
        pass

    # Also log to a standard log file
    log_dir = os.path.join(safe_root, "logs")
    os.makedirs(log_dir, exist_ok=True)
    log_file = os.path.join(log_dir, "containment.log")
    try:
        with open(log_file, "a") as f:
            f.write(f"[{time.strftime('%Y-%m-%d %H:%M:%S')}] BLOCKED WRITE ATTEMPT: {path}\n")
    except Exception:
        pass


def is_write_denied(path: str) -> bool:
    """Return True if path is blocked by the write denylist or safe root."""
    
    home = os.path.realpath(os.path.expanduser("~"))
    resolved = os.path.realpath(os.path.expanduser(str(path)))

    if resolved in build_write_denied_paths(home):
        return True
    for prefix in build_write_denied_prefixes(home):
        if resolved.startswith(prefix):
            return True

    # 1. Block writes to JoyZoning app source unless approved
    # The monorepo root is 2 levels up from apps/agent-runtime/agent/file_safety.py
    app_root = os.path.realpath(os.path.join(os.path.dirname(__file__), "..", ".."))
    if resolved.startswith(app_root + os.sep):
        workspaces_dir = os.path.join(app_root, ".joy-workspaces")
        is_workspace_file = resolved.startswith(workspaces_dir + os.sep)
        if not is_workspace_file:
            # Attempt to write to JoyZoning app source code!
            # Bypass check if YOLO mode is active
            if os.getenv("HERMES_YOLO_MODE") == "1":
                return False
                
            try:
                from tools.approval import prompt_dangerous_approval, is_current_session_yolo_enabled
                if is_current_session_yolo_enabled():
                    return False
                    
                choice = prompt_dangerous_approval(
                    command=f"Write to JoyZoning app source: {resolved}",
                    description=f"Attempting to write/modify JoyZoning app source file: {resolved}",
                    allow_permanent=False
                )
                if choice in ("once", "session", "always"):
                    return False
            except Exception:
                pass
                
            log_blocked_attempt(resolved)
            return True

    # 2. Workspace containment check (strict yard bounds)
    safe_root = get_safe_write_root()
    if safe_root:
        is_inside = (resolved == safe_root or resolved.startswith(safe_root + os.sep))
        if not is_inside:
            log_blocked_attempt(resolved)
            return True

    return False



def get_read_block_error(path: str) -> Optional[str]:
    """Return an error message when a read targets internal Hermes cache files."""
    resolved = Path(path).expanduser().resolve()
    hermes_home = _hermes_home_path().resolve()
    blocked_dirs = [
        hermes_home / "skills" / ".hub" / "index-cache",
        hermes_home / "skills" / ".hub",
    ]
    for blocked in blocked_dirs:
        try:
            resolved.relative_to(blocked)
        except ValueError:
            continue
        return (
            f"Access denied: {path} is an internal Hermes cache file "
            "and cannot be read directly to prevent prompt injection. "
            "Use the skills_list or skill_view tools instead."
        )
    return None
