#!/usr/bin/env python3
"""Fetch and format JoyZoning timeline events for the live progress UI."""
from __future__ import annotations

import json
import urllib.error
import urllib.request
from typing import Any

# Plain-language labels (like notification center / activity feed).
EVENT_LABELS: dict[str, str] = {
    "execution.lease.created": "Build slot claimed",
    "execution.lease.status_changed": "Build status updated",
    "execution.lease.merged": "Build merged",
    "execution.lease.revoked": "Build cancelled",
    "dietcode.execution.started": "AI worker started",
    "dietcode.execution.completed": "AI worker finished a step",
    "hermes.run.started": "AI session started",
    "hermes.run.completed": "AI session finished",
    "hermes.tool.started": "AI is using a tool",
    "hermes.tool.completed": "AI finished a tool",
    "hermes.message.delta": "AI is writing",
    "workspace.file.changed": "Project files updated",
    "git.status.changed": "Source control updated",
    "verification.report.attached": "Quality report submitted",
    "task.status_changed": "Task status changed",
}


def fetch_events(
    base_url: str,
    task_id: str,
    *,
    since_id: int = 0,
    limit: int = 12,
    timeout: float = 4.0,
) -> tuple[list[dict[str, Any]], int]:
    """Returns (events, max_id_seen)."""
    url = f"{base_url.rstrip('/')}/api/events?correlationId={task_id}&since={since_id}"
    try:
        with urllib.request.urlopen(url, timeout=timeout) as resp:
            data = json.loads(resp.read().decode())
    except (urllib.error.URLError, TimeoutError, json.JSONDecodeError, OSError):
        return [], since_id

    if not isinstance(data, list):
        return [], since_id

    events = data[-limit:]
    max_id = since_id
    for evt in events:
        if isinstance(evt, dict):
            eid = evt.get("id") or evt.get("Id")
            if isinstance(eid, int) and eid > max_id:
                max_id = eid
    return events, max_id


def friendly_event_line(evt: dict[str, Any]) -> str:
    etype = str(evt.get("type") or evt.get("Type") or "")
    label = EVENT_LABELS.get(etype, etype.replace(".", " ").replace("_", " ").title())
    payload_raw = evt.get("payloadJson") or evt.get("PayloadJson") or "{}"
    detail = ""
    try:
        payload = json.loads(payload_raw) if isinstance(payload_raw, str) else payload_raw
        if isinstance(payload, dict):
            for key in ("summary", "message", "toolName", "path", "status"):
                if payload.get(key):
                    detail = str(payload[key])[:80]
                    break
    except json.JSONDecodeError:
        pass
    if detail:
        return f"{label}: {detail}"
    return label


def merge_feed_lines(
    evidence_lines: list[str] | None,
    events: list[dict[str, Any]] | None,
    *,
    max_lines: int = 5,
) -> list[str]:
    lines: list[str] = []
    for evt in events or []:
        if isinstance(evt, dict):
            lines.append(friendly_event_line(evt))
    for line in evidence_lines or []:
        text = str(line).strip()
        if text and text not in lines:
            lines.append(text)
    return lines[-max_lines:]
