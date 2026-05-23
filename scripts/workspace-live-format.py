#!/usr/bin/env python3
"""Render JoyZoning live workspace progress for workspace-live.sh."""
from __future__ import annotations

import argparse
import json
import os
import sys
from datetime import datetime, timezone
from pathlib import Path

_LIB = Path(__file__).resolve().parent / "lib"
if str(_LIB) not in sys.path:
    sys.path.insert(0, str(_LIB))

from workspace_live_ui import (  # noqa: E402
    display_from_payload,
    fingerprint,
    recommend_poll,
    render_dashboard,
    render_pulse,
)


def load_state(path: Path) -> dict:
    if not path.is_file():
        return {}
    try:
        data = json.loads(path.read_text())
    except (OSError, json.JSONDecodeError):
        return {}
    if "payload" in data:
        return data
    return {"payload": data, "display": {}, "meta": {}}


def save_state(path: Path, payload: dict, display: dict, meta: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps({"payload": payload, "display": display, "meta": meta}, indent=2))


def update_meta(meta: dict, display: dict) -> dict:
    now = datetime.now(timezone.utc).timestamp()
    if not meta.get("watchStartedAt"):
        meta["watchStartedAt"] = datetime.now(timezone.utc).isoformat()
    hist: list = meta.setdefault("percentHistory", [])
    pct = int(display.get("progressPercent") or 0)
    if not hist or hist[-1][1] != pct:
        hist.append([now, pct])
        meta["percentHistory"] = hist[-20:]
    return meta


def render(
    payload: dict,
    *,
    state_path: Path,
    full: bool,
    clear: bool,
    simple: bool,
    show_paths: bool,
    compact: bool,
) -> tuple[int, bool]:
    display = display_from_payload(payload)
    prev_bundle = load_state(state_path)
    prev_display = prev_bundle.get("display") or {}
    meta = update_meta(dict(prev_bundle.get("meta") or {}), display)
    changed = fingerprint(payload, display) != fingerprint(
        prev_bundle.get("payload") or {}, prev_display
    )
    poll = recommend_poll(payload, display, meta)

    if payload.get("message") and not payload.get("leaseStatus"):
        from workspace_live_ui import c

        print(c("33", f"○ {payload.get('message')}"))
        save_state(state_path, payload, display, meta)
        return poll or 15, changed

    if not full and not changed:
        tick = int(prev_bundle.get("_tick", 0)) + 1
        line = render_pulse(display, payload, tick=tick, meta=meta, compact=compact)
        if compact and sys.stdout.isatty():
            print(f"\r{line:<110}", end="", flush=True)
        else:
            print(line, flush=True)
        save_state(state_path, {**payload, "_tick": tick}, display, meta)
        return poll, False

    if compact and sys.stdout.isatty():
        print()

    if clear and changed and sys.stdout.isatty():
        print("\033[2J\033[H", end="")

    lines = render_dashboard(
        payload,
        display=display,
        prev=prev_display,
        meta=meta,
        simple=simple,
        show_paths=show_paths,
    )
    print("\n".join(lines), flush=True)
    save_state(state_path, payload, display, meta)
    return poll, changed


def main() -> int:
    parser = argparse.ArgumentParser(description="JoyZoning live progress renderer")
    parser.add_argument("--state", required=True)
    parser.add_argument("--full", action="store_true")
    parser.add_argument("--clear", action="store_true")
    parser.add_argument("--simple", action="store_true")
    parser.add_argument("--paths", action="store_true")
    parser.add_argument(
        "--compact",
        action="store_true",
        help="Single-line in-place pulse between full refreshes",
    )
    parser.add_argument("--no-compact", action="store_true")
    parser.add_argument("--stdin", action="store_true")
    parser.add_argument("json_file", nargs="?")
    args = parser.parse_args()

    compact = args.compact
    if not args.no_compact and sys.stdout.isatty() and os.environ.get("JOYZONING_LIVE_COMPACT", "1") == "1":
        compact = True

    if args.stdin:
        raw = sys.stdin.read()
    elif args.json_file:
        raw = Path(args.json_file).read_text()
    else:
        print("Provide --stdin or json_file", file=sys.stderr)
        return 2

    payload = json.loads(raw)
    poll, _ = render(
        payload,
        state_path=Path(args.state),
        full=args.full,
        clear=args.clear,
        simple=args.simple,
        show_paths=args.paths,
        compact=compact,
    )
    poll_file = os.environ.get("JOYZONING_POLL_FILE")
    if poll_file:
        Path(poll_file).write_text(str(poll))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
