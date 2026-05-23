#!/usr/bin/env python3
"""Human-friendly live progress UI — delivery-tracker / install-wizard patterns."""
from __future__ import annotations

import os
import sys
from datetime import datetime, timezone
from typing import Any

STEP_PENDING = "pending"
STEP_CURRENT = "current"
STEP_COMPLETE = "complete"
STEP_FAILED = "failed"

TERMINAL_STATUSES = frozenset({"ReadyForReview", "Merged", "Revoked", "Completed", "Cancelled", "Failed"})

PIPELINE = (
    ("start", "Getting started"),
    ("build", "Building your project"),
    ("verify", "Running quality checks"),
    ("review", "Ready for your review"),
)

STATUS_STEP_INDEX = {
    "Leased": 0,
    "Running": 1,
    "Blocked": 1,
    "Verifying": 2,
    "ReadyForReview": 3,
    "Merged": 3,
}

SPINNER_FRAMES = "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏"


def use_color() -> bool:
    return sys.stdout.isatty() and os.environ.get("NO_COLOR") is None


def c(code: str, text: str) -> str:
    if not use_color():
        return text
    return f"\033[{code}m{text}\033[0m"


def icon(name: str) -> str:
    if not use_color():
        return {"ok": "[OK]", "run": "[..]", "wait": "[ ]", "fail": "[!]", "spin": "[~]"}.get(name, "")
    return {"ok": "✓", "run": "●", "wait": "○", "fail": "✗", "spin": "◐"}.get(name, "·")


def spinner(frame: int) -> str:
    if not use_color():
        return "~"
    return SPINNER_FRAMES[frame % len(SPINNER_FRAMES)]


def display_from_payload(payload: dict[str, Any]) -> dict[str, Any]:
    d = payload.get("display")
    if isinstance(d, dict) and d.get("headline"):
        return _normalize_display(d, payload)
    return synthesize_display(payload)


def _normalize_display(d: dict[str, Any], payload: dict[str, Any]) -> dict[str, Any]:
    steps = d.get("steps") or []
    step_count = int(d.get("stepCount") or len(steps) or len(PIPELINE))
    cur = int(d.get("currentStepIndex") or 0)
    if not d.get("stepProgressLabel"):
        d = {**d, "stepProgressLabel": f"Step {cur + 1} of {step_count}"}
    if not d.get("currentStepTitle") and steps:
        for s in steps:
            if s.get("state") == STEP_CURRENT:
                d = {**d, "currentStepTitle": s.get("label")}
                break
    if "pollMode" not in d:
        d = {**d, "pollMode": _infer_poll_mode(payload, d)}
    return d


def _infer_poll_mode(payload: dict[str, Any], display: dict[str, Any]) -> str:
    if payload.get("leaseStatus") in TERMINAL_STATUSES:
        return "stopped"
    activity = display.get("activityState", "")
    if activity == "active":
        return "burst"
    if activity in ("idle", "stuck", "blocked"):
        return "slow"
    return "normal"


def synthesize_display(payload: dict[str, Any]) -> dict[str, Any]:
    status = payload.get("leaseStatus") or ""
    p = payload.get("progress") or {}
    steps = []
    idx = STATUS_STEP_INDEX.get(status, 0)
    failed = status == "Blocked"
    for i, (sid, label) in enumerate(PIPELINE):
        if failed and i == idx:
            st = STEP_FAILED
        elif i < idx:
            st = STEP_COMPLETE
        elif i == idx:
            st = STEP_CURRENT
        else:
            st = STEP_PENDING
        steps.append({"id": sid, "label": label, "state": st})

    deliverables = [
        {"id": "package", "label": "Project setup", "done": bool(p.get("hasPackageJson")), "count": None},
        {"id": "screens", "label": "App screens", "done": (p.get("appScreens") or 0) > 0, "count": p.get("appScreens")},
        {"id": "features", "label": "App features", "done": (p.get("featureFiles") or 0) > 0, "count": p.get("featureFiles")},
        {"id": "shared", "label": "Shared code", "done": (p.get("sharedFiles") or 0) > 0, "count": p.get("sharedFiles")},
        {"id": "readme", "label": "README guide", "done": bool(p.get("hasReadme")), "count": None},
    ]
    done = sum(1 for x in deliverables if x["done"])
    percent = min(100, int((idx + 0.5) / 4 * 70 + done / max(len(deliverables), 1) * 30))

    activity = "blocked" if status == "Blocked" else ("active" if status == "Running" else "waiting")
    if status == "ReadyForReview":
        activity = "review"

    headline = "Working on your project"
    if status == "Blocked":
        headline = "Paused — needs your attention"
    elif status == "ReadyForReview":
        headline = "Ready for you to review"
    elif status == "Running":
        headline = "Building your project"

    current_title = steps[idx]["label"] if idx < len(steps) else None

    return {
        "headline": headline,
        "subheadline": payload.get("title") or payload.get("message") or "",
        "activityState": activity,
        "progressPercent": percent,
        "currentStepIndex": idx,
        "stepCount": len(PIPELINE),
        "stepProgressLabel": f"Step {idx + 1} of {len(PIPELINE)}",
        "currentStepTitle": current_title,
        "timeGuidance": _default_time_guidance(activity, status),
        "staleWarning": None,
        "pollMode": _infer_poll_mode(payload, {"activityState": activity}),
        "steps": steps,
        "deliverables": deliverables,
        "nextActions": [],
        "recentActivity": list(payload.get("recentEvidence") or [])[-3:],
        "helpTips": _default_help_tips(activity, status),
    }


def _default_time_guidance(activity: str, status: str) -> str:
    return {
        "waiting": "Starting the AI worker typically takes 1–3 minutes.",
        "active": "Active builds often take 10–30 minutes depending on app size.",
        "idle": "Quiet periods are normal while the AI thinks or runs tools.",
        "stuck": "If nothing changes for 10+ minutes, check tips below or logs.",
        "blocked": "Fix the issue, then recover and run the task again.",
        "review": "Open your project folder when you're ready to review.",
        "none": "Dispatch a task to start.",
    }.get(activity, "Most app builds take 10–30 minutes end-to-end.")


def _default_help_tips(activity: str, status: str) -> list[str]:
    tips = [
        "Files appear in your project folder as they are written.",
        "You can close this window — the build keeps running.",
    ]
    if activity in ("active", "idle", "stuck"):
        tips.append("Quiet minutes are normal during long AI steps.")
    if status == "ReadyForReview":
        tips.append("Nothing merges until you approve the result.")
    return tips


def render_timeline(steps: list[dict[str, Any]]) -> str:
    """Horizontal tracker: ●──●──○──○ (like package delivery maps)."""
    if not steps:
        return ""
    chars: list[str] = []
    for i, step in enumerate(steps):
        state = step.get("state", STEP_PENDING)
        if state == STEP_COMPLETE:
            chars.append(c("32", "●"))
        elif state == STEP_CURRENT:
            chars.append(c("36", "◉"))
        elif state == STEP_FAILED:
            chars.append(c("31", "✗"))
        else:
            chars.append(c("90", "○"))
        if i < len(steps) - 1:
            chars.append(c("90", "──"))
    return "".join(chars)


def progress_bar(percent: int, width: int = 28) -> str:
    filled = min(width, max(0, int(width * percent / 100)))
    return f"[{('█' * filled).ljust(width, '░')}] {percent:>3}%"


def step_line(step: dict[str, Any], index: int, current_index: int) -> str:
    state = step.get("state", STEP_PENDING)
    label = step.get("label", "?")
    num = index + 1
    if state == STEP_COMPLETE:
        mark = icon("ok")
    elif state == STEP_FAILED:
        mark = icon("fail")
    elif state == STEP_CURRENT:
        mark = icon("spin")
    else:
        mark = icon("wait")
    suffix = c("36", "  ← now") if index == current_index and state == STEP_CURRENT else ""
    return f"  {num}. {mark}  {label}{suffix}"


def activity_badge(activity: str) -> str:
    labels = {
        "active": ("Building", "32"),
        "idle": ("Working quietly", "33"),
        "stuck": ("Slow / quiet", "33"),
        "waiting": ("Starting", "36"),
        "blocked": ("Needs you", "31"),
        "review": ("Review", "35"),
        "done": ("Done", "32"),
        "none": ("Waiting", "37"),
    }
    text, color = labels.get(activity, ("Working", "37"))
    return c(color, text)


def fmt_duration(seconds: int) -> str:
    if seconds < 60:
        return f"{seconds}s"
    if seconds < 3600:
        return f"{seconds // 60}m {seconds % 60}s"
    return f"{seconds // 3600}h {(seconds % 3600) // 60}m"


def fmt_ago(iso: str | None) -> str:
    if not iso:
        return ""
    try:
        ts = datetime.fromisoformat(iso.replace("Z", "+00:00"))
    except ValueError:
        return iso
    return fmt_duration(int((datetime.now(timezone.utc) - ts).total_seconds())) + " ago"


def fingerprint(payload: dict[str, Any], display: dict[str, Any]) -> str:
    p = payload.get("progress") or {}
    return "|".join(
        str(x)
        for x in [
            payload.get("leaseStatus"),
            display.get("headline"),
            display.get("progressPercent"),
            display.get("activityState"),
            display.get("stepProgressLabel"),
            p.get("appScreens"),
            p.get("featureFiles"),
            p.get("filesCopiedThisTick"),
            (display.get("recentActivity") or [""])[-1:],
        ]
    )


def recommend_poll(payload: dict[str, Any], display: dict[str, Any], meta: dict[str, Any]) -> int:
    api = payload.get("recommendedPollSeconds")
    mode = display.get("pollMode") or _infer_poll_mode(payload, display)

    if payload.get("leaseStatus") in TERMINAL_STATUSES:
        return 0

    if isinstance(api, int) and api > 0:
        base = api
    else:
        status = payload.get("leaseStatus") or ""
        base = {"Running": 5, "Leased": 8, "Verifying": 12, "Blocked": 25}.get(status, 10)

    # Velocity boost: percent climbing → poll faster
    hist = meta.get("percentHistory") or []
    if len(hist) >= 2 and hist[-1][1] > hist[-2][1]:
        base = min(base, 3)

    if mode == "burst":
        return min(base, 3)
    if mode == "slow":
        return max(base, 20)
    return base


def estimate_eta(meta: dict[str, Any], percent: int) -> str | None:
    hist: list[list[Any]] = meta.get("percentHistory") or []
    if percent <= 0 or percent >= 100 or len(hist) < 2:
        return None
    t0, p0 = hist[0][0], hist[0][1]
    t1, p1 = hist[-1][0], hist[-1][1]
    if p1 <= p0:
        return None
    rate = (p1 - p0) / max(1, t1 - t0)
    remaining = (100 - percent) / rate
    if remaining > 7200:
        return None
    return f"~{fmt_duration(int(remaining))} left (estimate)"


def render_pulse(
    display: dict[str, Any],
    payload: dict[str, Any],
    *,
    tick: int,
    meta: dict[str, Any],
    compact: bool,
) -> str:
    p = payload.get("progress") or {}
    activity = display.get("activityState", "")
    pct = int(display.get("progressPercent") or 0)
    step_lbl = display.get("stepProgressLabel", "")
    elapsed = ""
    if meta.get("watchStartedAt"):
        try:
            start = datetime.fromisoformat(meta["watchStartedAt"])
            elapsed = fmt_duration(int((datetime.now(timezone.utc) - start).total_seconds()))
        except ValueError:
            pass

    if compact:
        parts = [
            spinner(tick),
            step_lbl,
            f"{pct}%",
            activity_badge(activity),
        ]
        if elapsed:
            parts.append(f"watching {elapsed}")
        if (p.get("filesCopiedThisTick") or 0) > 0:
            parts.append(f"+{p['filesCopiedThisTick']} files")
        eta = estimate_eta(meta, pct)
        if eta:
            parts.append(eta)
        line = "  ".join(parts)
        return line[:110]

    parts = [
        datetime.now(timezone.utc).strftime("%H:%M:%S"),
        spinner(tick),
        step_lbl,
        f"{pct}%",
        activity_badge(activity),
    ]
    if elapsed:
        parts.append(f"elapsed {elapsed}")
    if (p.get("filesCopiedThisTick") or 0) > 0:
        parts.append(f"+{p['filesCopiedThisTick']} synced")
    wt = p.get("worktreeLastWriteUtc")
    if wt:
        parts.append(f"saved {fmt_ago(wt)}")
    return " · ".join(parts)


def render_dashboard(
    payload: dict[str, Any],
    *,
    display: dict[str, Any],
    prev: dict[str, Any],
    meta: dict[str, Any],
    simple: bool,
    show_paths: bool,
) -> list[str]:
    lines: list[str] = []
    activity = display.get("activityState", "none")
    pct = int(display.get("progressPercent") or 0)

    lines.append(c("1", "═" * 58))
    lines.append(c("1", "  JOYZONING BUILD TRACKER"))
    lines.append(c("1", "═" * 58))
    lines.append("")
    lines.append(c("1", display.get("headline", "JoyZoning")))
    lines.append(f"  {display.get('subheadline', '')}")
    lines.append("")
    lines.append(f"  {c('1', display.get('stepProgressLabel', ''))}  ·  {display.get('currentStepTitle', '')}")
    lines.append(f"  {render_timeline(display.get('steps') or [])}")
    lines.append(f"  Status: {activity_badge(activity)}")
    if display.get("timeGuidance"):
        lines.append(c("90", f"  ℹ {display['timeGuidance']}"))
    if display.get("staleWarning"):
        lines.append(c("33", f"  ⚠ {display['staleWarning']}"))
    lines.append("")
    lines.append("  " + progress_bar(pct))
    eta = estimate_eta(meta, pct)
    if eta:
        lines.append(c("90", f"  {eta}"))
    lines.append("")

    if not simple:
        lines.append(c("1", "  Build steps"))
        cur_idx = int(display.get("currentStepIndex") or 0)
        for i, step in enumerate(display.get("steps") or []):
            lines.append(step_line(step, i, cur_idx))
        lines.append("")

    lines.append(c("1", "  In your project folder"))
    prev_d = {x.get("id"): x for x in (prev.get("deliverables") or [])}
    for item in display.get("deliverables") or []:
        done = item.get("done")
        mark = icon("ok") if done else icon("wait")
        label = item.get("label", "?")
        count = item.get("count")
        extra = f" ({count})" if count else ""
        delta = ""
        pid = item.get("id")
        if pid in prev_d and prev_d[pid].get("count") != count and count:
            old = prev_d[pid].get("count") or 0
            if count > old:
                delta = c("32", f" +{count - old}")
        lines.append(f"    {mark}  {label}{extra}{delta}")

    p = payload.get("progress") or {}
    wt = p.get("worktreeLastWriteUtc")
    if wt:
        lines.append(f"\n    Last file saved: {fmt_ago(wt)}")
    mirrored = p.get("filesCopiedThisTick") or 0
    if mirrored:
        lines.append(c("36", f"    {mirrored} file(s) copied to your folder just now"))

    actions = display.get("nextActions") or []
    if actions:
        lines.append("")
        lines.append(c("1", "  What to do next"))
        for i, action in enumerate(actions, 1):
            lines.append(f"    {i}. {action}")

    activity_lines = display.get("recentActivity") or []
    if activity_lines and not simple:
        lines.append("")
        lines.append(c("1", "  Recent activity"))
        for line in activity_lines[-4:]:
            text = str(line)[:100]
            lines.append(f"    • {text}{'…' if len(str(line)) > 100 else ''}")

    tips = display.get("helpTips") or []
    if tips and not simple:
        lines.append("")
        lines.append(c("1", "  Good to know"))
        for tip in tips[:3]:
            lines.append(c("90", f"    · {tip}"))

    if show_paths:
        lines.append("")
        lines.append(c("90", "  ── folders ──"))
        if payload.get("sessionWorkspaceRoot"):
            lines.append(c("90", f"    Project: {payload['sessionWorkspaceRoot']}"))
        if payload.get("liveFile"):
            lines.append(c("90", f"    Status:  {payload['liveFile']}"))
        json_rel = os.path.join(payload.get("sessionWorkspaceRoot") or "", ".joyzoning/live.json")
        lines.append(c("90", f"    JSON:    {json_rel}"))

    poll = recommend_poll(payload, display, meta)
    mode = display.get("pollMode", "normal")
    if meta.get("watchStartedAt"):
        try:
            start = datetime.fromisoformat(meta["watchStartedAt"])
            w = fmt_duration(int((datetime.now(timezone.utc) - start).total_seconds()))
            lines.append("")
            lines.append(c("90", f"  Watching for {w} · refresh ~{poll}s ({mode}) · Ctrl+C to stop"))
        except ValueError:
            pass
    lines.append(c("90", "  Tip: open JOYZONING_LIVE.md in your project folder anytime"))
    lines.append(c("1", "═" * 58))
    return lines
