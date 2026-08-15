#!/usr/bin/env python3
"""Boundedly audit and recover an explicit registry of external workers."""

from __future__ import annotations

import argparse
import concurrent.futures
import json
import os
import pathlib
import sys
import time

from external_worker_runtime import (
    ASSIGNMENT_SCHEMA,
    WATCHDOG_REGISTRY_SCHEMA,
    WATCHDOG_STATE_SCHEMA,
    ProcessIdentityError,
    atomic_write_json,
    audit_attempt,
    read_json_record,
    utc_now,
)
from launch_role import relaunch_assignment


DEFAULT_POLL_SECONDS = 30.0
MAX_POLL_SECONDS = 60.0


def read_registry(path: pathlib.Path) -> list[tuple[str, pathlib.Path]]:
    """Read exact assignment roots without discovering worktrees or evidence."""
    record = read_json_record(path.resolve(strict=True))
    if record.get("schema") != WATCHDOG_REGISTRY_SCHEMA:
        raise ProcessIdentityError("watchdog registry has an unknown schema")
    entries = record.get("assignments")
    if not isinstance(entries, list):
        raise ProcessIdentityError("watchdog registry assignments must be a list")
    result: list[tuple[str, pathlib.Path]] = []
    seen_ids: set[str] = set()
    seen_roots: set[pathlib.Path] = set()
    for entry in entries:
        if not isinstance(entry, dict):
            raise ProcessIdentityError("watchdog registry entry must be an object")
        if entry.get("enabled", True) is not True:
            continue
        assignment_id = entry.get("assignment_id")
        root_value = entry.get("assignment_root")
        if not isinstance(assignment_id, str) or not isinstance(root_value, str):
            raise ProcessIdentityError("watchdog registry entry lacks identity/root")
        root = pathlib.Path(root_value).resolve(strict=True)
        assignment = read_json_record(root / "assignment.json")
        if assignment.get("schema") != ASSIGNMENT_SCHEMA:
            raise ProcessIdentityError(f"assignment record has unknown schema: {root}")
        if assignment.get("assignment_id") != assignment_id:
            raise ProcessIdentityError(f"registry identity does not match assignment: {root}")
        if assignment_id in seen_ids or root in seen_roots:
            raise ProcessIdentityError("watchdog registry contains a duplicate assignment")
        seen_ids.add(assignment_id)
        seen_roots.add(root)
        result.append((assignment_id, root))
    return result


def result_signature(result: dict[str, object]) -> dict[str, object]:
    """Select state-transition facts; omit volatile ages and process observations."""
    return {
        key: result.get(key)
        for key in ("event", "reason", "attempt_id", "generation", "status")
        if key in result
    }


def audit_registry_once(
    registry: pathlib.Path,
    *,
    state_path: pathlib.Path,
    resource_lock_dir: pathlib.Path | None,
    workers: int,
    lease_timeout_seconds: float,
    launch_stale_seconds: float,
    resolve_grace_seconds: float,
    resolve_kill_seconds: float,
) -> list[dict[str, object]]:
    """Audit every registered assignment independently and return changed states."""
    assignments = read_registry(registry)
    try:
        prior = read_json_record(state_path) if state_path.exists() else {}
    except ProcessIdentityError:
        prior = {}
    prior_states = (
        prior.get("states") if prior.get("schema") == WATCHDOG_STATE_SCHEMA else {}
    )
    if not isinstance(prior_states, dict):
        prior_states = {}

    def run(item: tuple[str, pathlib.Path]) -> tuple[str, dict[str, object]]:
        assignment_id, root = item
        try:
            result = audit_attempt(
                root,
                lease_timeout_seconds=lease_timeout_seconds,
                resource_lock_dir=resource_lock_dir,
                recover=relaunch_assignment,
                resolve_grace_seconds=resolve_grace_seconds,
                resolve_kill_seconds=resolve_kill_seconds,
                launch_stale_seconds=launch_stale_seconds,
            )
        except (OSError, ProcessIdentityError, TimeoutError, ValueError) as error:
            result = {"event": "audit-error", "reason": str(error)}
        return assignment_id, {"assignment_root": str(root), **result}

    current: dict[str, dict[str, object]] = {}
    # A bounded shared pool prevents one contended assignment from starving others.
    with concurrent.futures.ThreadPoolExecutor(max_workers=max(1, workers)) as executor:
        futures = [executor.submit(run, item) for item in assignments]
        for future in concurrent.futures.as_completed(futures):
            assignment_id, result = future.result()
            current[assignment_id] = result_signature(result)

    changed = []
    for assignment_id, root in assignments:
        signature = current[assignment_id]
        if prior_states.get(assignment_id) != signature:
            changed.append(
                {
                    "observed_utc": utc_now(),
                    "assignment_id": assignment_id,
                    "assignment_root": str(root),
                    **signature,
                }
            )
    atomic_write_json(
        state_path,
        {
            "schema": WATCHDOG_STATE_SCHEMA,
            "registry": str(registry.resolve()),
            "states": current,
            "updated_utc": utc_now(),
        },
    )
    return changed


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--registry", required=True, type=pathlib.Path)
    parser.add_argument("--state", type=pathlib.Path)
    parser.add_argument("--event-log", type=pathlib.Path)
    parser.add_argument("--resource-lock-dir", type=pathlib.Path)
    parser.add_argument("--poll-seconds", type=float, default=DEFAULT_POLL_SECONDS)
    parser.add_argument("--lease-timeout", type=float, default=.25)
    parser.add_argument("--launch-stale", type=float, default=30.0)
    parser.add_argument("--resolve-grace", type=float, default=2.0)
    parser.add_argument("--resolve-kill", type=float, default=2.0)
    parser.add_argument("--workers", type=int, default=4)
    parser.add_argument("--once", action="store_true")
    args = parser.parse_args()
    if not 0 < args.poll_seconds <= MAX_POLL_SECONDS:
        parser.error(f"--poll-seconds must be in (0, {MAX_POLL_SECONDS}]")
    if args.workers < 1 or args.workers > 32:
        parser.error("--workers must be between 1 and 32")
    state_path = args.state or args.registry.with_suffix(
        args.registry.suffix + ".watchdog-state.json"
    )
    try:
        while True:
            started = time.monotonic()
            changed = audit_registry_once(
                args.registry,
                state_path=state_path,
                resource_lock_dir=args.resource_lock_dir,
                workers=args.workers,
                lease_timeout_seconds=args.lease_timeout,
                launch_stale_seconds=args.launch_stale,
                resolve_grace_seconds=args.resolve_grace,
                resolve_kill_seconds=args.resolve_kill,
            )
            for event in changed:
                line = json.dumps(event, sort_keys=True)
                print(line, flush=True)
                if args.event_log is not None:
                    args.event_log.parent.mkdir(parents=True, exist_ok=True)
                    with args.event_log.open("a", encoding="utf-8") as stream:
                        stream.write(line + "\n")
                        stream.flush()
                        os.fsync(stream.fileno())
            if args.once:
                return 0
            time.sleep(max(0.0, args.poll_seconds - (time.monotonic() - started)))
    except (OSError, ProcessIdentityError, TimeoutError, ValueError) as error:
        print(json.dumps({"event": "watchdog-error", "reason": str(error)}), file=sys.stderr)
        return 75
    except KeyboardInterrupt:
        return 130


if __name__ == "__main__":
    sys.exit(main())
